use std::collections::HashMap;
use std::ffi::{CStr, CString};
use std::net::{SocketAddr, ToSocketAddrs};
use std::os::raw::{c_char, c_int};
use std::ptr;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{mpsc, Arc, Mutex, OnceLock};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

use quinn::crypto::rustls::QuicClientConfig;
use quinn::{ClientConfig, Connection, Endpoint, RecvStream, SendStream, VarInt, WriteError};
use rustls::client::danger::{HandshakeSignatureValid, ServerCertVerified, ServerCertVerifier};
use rustls::{DigitallySignedStruct, Error as RustlsError, SignatureScheme};
use rustls_pki_types::{CertificateDer, ServerName, UnixTime};
use serde_json::json;
use tokio::runtime::{Builder, Runtime};
use tokio::time::timeout;

const HEADER_LENGTH: usize = 40;
const VERSION_MAJOR: u8 = 1;
const CURRENT_WIRE_FORMAT: u8 = 0;
const ALPN_CURRENT: &[u8] = b"nnrp/1";
const MSG_CLIENT_HELLO: u8 = 0x01;
const MSG_SERVER_HELLO_ACK: u8 = 0x02;
const MSG_CLOSE: u8 = 0x05;
const MSG_FRAME_SUBMIT: u8 = 0x10;
const MSG_RESULT_PUSH: u8 = 0x12;
const MSG_FLOW_UPDATE: u8 = 0x17;
const MSG_RESULT_HINT: u8 = 0x18;
const MSG_TRANSPORT_PROBE_ACK: u8 = 0x1A;
const MSG_PONG: u8 = 0x21;
const FLAG_ACK_REQUIRED: u32 = 0x0000_0001;
const DEFAULT_IDLE_TIMEOUT_SECONDS: u64 = 120;
const DEFAULT_KEEPALIVE_INTERVAL_SECONDS: u64 = 10;
const DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS: u64 = 10;
const DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS: u64 = 10;

static NEXT_CLIENT_HANDLE: AtomicU64 = AtomicU64::new(1);
static ACTIVE_CLIENTS: OnceLock<Mutex<HashMap<u64, NativeClientWorker>>> = OnceLock::new();

struct NativeClientWorker {
    command_tx: mpsc::Sender<NativeClientCommand>,
    worker_thread: JoinHandle<()>,
}

enum NativeClientCommand {
    BeginSubmit {
        submit_packet: Vec<u8>,
        response_tx: mpsc::Sender<Result<(), String>>,
    },
    ReceiveResult {
        response_tx: mpsc::Sender<Result<Vec<u8>, String>>,
    },
    ReceiveSessionPacket {
        response_tx: mpsc::Sender<Result<Vec<u8>, String>>,
    },
    Submit {
        submit_packet: Vec<u8>,
        response_tx: mpsc::Sender<Result<NativeSubmitResult, String>>,
    },
    Ping {
        ping_packet: Vec<u8>,
        response_tx: mpsc::Sender<Result<Vec<u8>, String>>,
    },
    Cancel {
        cancel_packet: Vec<u8>,
        response_tx: mpsc::Sender<Result<(), String>>,
    },
    Close {
        response_tx: mpsc::Sender<Result<(), String>>,
    },
}

struct NativeClient {
    runtime: Runtime,
    endpoint: Endpoint,
    connection: Connection,
    control_send: SendStream,
    control_recv: RecvStream,
    negotiated_session_id: u32,
    negotiated_wire_format: u8,
    active_model_name: String,
}

struct NativeSubmitResult {
    result_packet: Vec<u8>,
    open_submit_stream_ms: f64,
    write_submit_packet_ms: f64,
    finish_submit_stream_ms: f64,
    accept_result_stream_ms: f64,
    read_result_packet_ms: f64,
    read_result_header_ms: f64,
    read_result_payload_ms: f64,
    quic_rtt_before_ms: f64,
    quic_rtt_after_accept_ms: f64,
    quic_rtt_after_read_ms: f64,
    quic_cwnd_before_bytes: u64,
    quic_cwnd_after_accept_bytes: u64,
    quic_cwnd_after_read_bytes: u64,
    quic_sent_packets_during_accept: u64,
    quic_lost_packets_during_accept: u64,
    quic_congestion_events_during_accept: u64,
    quic_sent_packets_total: u64,
    quic_lost_packets_total: u64,
    quic_congestion_events_total: u64,
}

#[derive(Clone, Copy)]
struct NativeQuicPathSnapshot {
    rtt_ms: f64,
    cwnd_bytes: u64,
    sent_packets: u64,
    lost_packets: u64,
    congestion_events: u64,
}

#[derive(Debug)]
struct SkipServerVerification;

impl ServerCertVerifier for SkipServerVerification {
    fn verify_server_cert(
        &self,
        _end_entity: &CertificateDer<'_>,
        _intermediates: &[CertificateDer<'_>],
        _server_name: &ServerName<'_>,
        _ocsp_response: &[u8],
        _now: UnixTime,
    ) -> Result<ServerCertVerified, RustlsError> {
        Ok(ServerCertVerified::assertion())
    }

    fn verify_tls12_signature(
        &self,
        _message: &[u8],
        _cert: &CertificateDer<'_>,
        _dss: &DigitallySignedStruct,
    ) -> Result<HandshakeSignatureValid, RustlsError> {
        Ok(HandshakeSignatureValid::assertion())
    }

    fn verify_tls13_signature(
        &self,
        _message: &[u8],
        _cert: &CertificateDer<'_>,
        _dss: &DigitallySignedStruct,
    ) -> Result<HandshakeSignatureValid, RustlsError> {
        Ok(HandshakeSignatureValid::assertion())
    }

    fn supported_verify_schemes(&self) -> Vec<SignatureScheme> {
        vec![
            SignatureScheme::RSA_PKCS1_SHA256,
            SignatureScheme::ECDSA_NISTP256_SHA256,
            SignatureScheme::RSA_PSS_SHA256,
            SignatureScheme::ED25519,
        ]
    }
}

fn active_clients() -> &'static Mutex<HashMap<u64, NativeClientWorker>> {
    ACTIVE_CLIENTS.get_or_init(|| Mutex::new(HashMap::new()))
}

fn build_runtime() -> Result<Runtime, String> {
    Builder::new_current_thread()
        .enable_all()
        .build()
        .map_err(|error| format!("failed to build tokio runtime: {error}"))
}

fn build_client_config(
    requested_wire_format: u8,
) -> Result<ClientConfig, String> {
    let crypto = rustls::ClientConfig::builder()
        .dangerous()
        .with_custom_certificate_verifier(Arc::new(SkipServerVerification))
        .with_no_client_auth();
    let mut crypto = crypto;
    crypto.alpn_protocols = build_alpn_protocols(requested_wire_format)?;

    let quic_crypto = QuicClientConfig::try_from(crypto)
        .map_err(|error| format!("failed to build rustls QUIC config: {error}"))?;
    let mut transport = quinn::TransportConfig::default();
    transport.max_idle_timeout(Some(
        quinn::IdleTimeout::try_from(Duration::from_secs(DEFAULT_IDLE_TIMEOUT_SECONDS))
            .map_err(|error| format!("failed to configure QUIC idle timeout: {error}"))?,
    ));
    transport.keep_alive_interval(Some(Duration::from_secs(
        DEFAULT_KEEPALIVE_INTERVAL_SECONDS,
    )));

    let mut client_config = ClientConfig::new(Arc::new(quic_crypto));
    client_config.transport_config(Arc::new(transport));
    Ok(client_config)
}

fn build_alpn_protocols(
    requested_wire_format: u8,
) -> Result<Vec<Vec<u8>>, String> {
    if requested_wire_format != CURRENT_WIRE_FORMAT {
        return Err(format!(
            "requested_wire_format must be {}",
            CURRENT_WIRE_FORMAT
        ));
    }

    Ok(vec![ALPN_CURRENT.to_vec()])
}

#[no_mangle]
pub extern "C" fn nnrp_quic_client_open(
    host: *const c_char,
    port: u16,
    tls_server_name: *const c_char,
    requested_model: *const c_char,
    requested_session_id: u32,
    requested_wire_format: u8,
    out_handle: *mut u64,
    out_negotiated_session_id: *mut u32,
    out_negotiated_wire_format: *mut u8,
    out_active_model_name: *mut *mut c_char,
    out_error: *mut *mut c_char,
) -> c_int {
    if out_handle.is_null()
        || out_negotiated_session_id.is_null()
        || out_negotiated_wire_format.is_null()
        || out_active_model_name.is_null()
        || out_error.is_null()
    {
        return 2;
    }

    unsafe {
        *out_handle = 0;
        *out_negotiated_session_id = 0;
        *out_negotiated_wire_format = 0;
        *out_active_model_name = ptr::null_mut();
        *out_error = ptr::null_mut();
    }

    let result = (|| -> Result<(u64, u32, u8, String), String> {
        let host = read_required_c_string(host, "host")?;
        let tls_server_name = read_required_c_string(tls_server_name, "tls_server_name")?;
        let requested_model = read_required_c_string(requested_model, "requested_model")?;
        let (client, negotiated_session_id, negotiated_wire_format, active_model_name) =
            spawn_native_client_worker(
                &host,
                port,
                &tls_server_name,
                &requested_model,
                requested_session_id,
                requested_wire_format,
            )?;

        let handle = NEXT_CLIENT_HANDLE.fetch_add(1, Ordering::Relaxed);
        active_clients()
            .lock()
            .map_err(|_| "native client registry lock poisoned".to_string())?
            .insert(handle, client);
        Ok((
            handle,
            negotiated_session_id,
            negotiated_wire_format,
            active_model_name,
        ))
    })();

    match result {
        Ok((handle, negotiated_session_id, negotiated_wire_format, active_model_name)) => unsafe {
            *out_handle = handle;
            *out_negotiated_session_id = negotiated_session_id;
            *out_negotiated_wire_format = negotiated_wire_format;
            write_c_string(out_active_model_name, &active_model_name);
            0
        },
        Err(error_text) => unsafe {
            write_c_string(out_error, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_client_probe(
    host: *const c_char,
    port: u16,
    tls_server_name: *const c_char,
    probe_packet: *const u8,
    probe_packet_len: c_int,
    requested_wire_format: u8,
    out_response_packet: *mut *mut u8,
    out_response_packet_len: *mut c_int,
    out_error: *mut *mut c_char,
) -> c_int {
    if host.is_null()
        || tls_server_name.is_null()
        || probe_packet.is_null()
        || probe_packet_len <= 0
        || out_response_packet.is_null()
        || out_response_packet_len.is_null()
        || out_error.is_null()
    {
        return 2;
    }

    unsafe {
        *out_response_packet = ptr::null_mut();
        *out_response_packet_len = 0;
        *out_error = ptr::null_mut();
    }

    let probe_packet =
        unsafe { std::slice::from_raw_parts(probe_packet, probe_packet_len as usize) };
    let result = (|| -> Result<Vec<u8>, String> {
        let host = read_required_c_string(host, "host")?;
        let tls_server_name = read_required_c_string(tls_server_name, "tls_server_name")?;
        probe_native_quic(
            &host,
            port,
            &tls_server_name,
            probe_packet,
            requested_wire_format,
        )
    })();

    match result {
        Ok(mut response_packet) => unsafe {
            *out_response_packet_len = response_packet.len() as c_int;
            *out_response_packet = response_packet.as_mut_ptr();
            std::mem::forget(response_packet);
            0
        },
        Err(error_text) => unsafe {
            write_c_string(out_error, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_client_submit(
    handle: u64,
    submit_packet: *const u8,
    submit_packet_len: c_int,
    out_result_packet: *mut *mut u8,
    out_result_packet_len: *mut c_int,
    out_open_submit_stream_ms: *mut f64,
    out_write_submit_packet_ms: *mut f64,
    out_finish_submit_stream_ms: *mut f64,
    out_accept_result_stream_ms: *mut f64,
    out_read_result_packet_ms: *mut f64,
    out_read_result_header_ms: *mut f64,
    out_read_result_payload_ms: *mut f64,
    out_quic_rtt_before_ms: *mut f64,
    out_quic_rtt_after_accept_ms: *mut f64,
    out_quic_rtt_after_read_ms: *mut f64,
    out_quic_cwnd_before_bytes: *mut u64,
    out_quic_cwnd_after_accept_bytes: *mut u64,
    out_quic_cwnd_after_read_bytes: *mut u64,
    out_quic_sent_packets_during_accept: *mut u64,
    out_quic_lost_packets_during_accept: *mut u64,
    out_quic_congestion_events_during_accept: *mut u64,
    out_quic_sent_packets_total: *mut u64,
    out_quic_lost_packets_total: *mut u64,
    out_quic_congestion_events_total: *mut u64,
    out_error: *mut *mut c_char,
) -> c_int {
    if handle == 0
        || submit_packet.is_null()
        || submit_packet_len <= 0
        || out_result_packet.is_null()
        || out_result_packet_len.is_null()
        || out_open_submit_stream_ms.is_null()
        || out_write_submit_packet_ms.is_null()
        || out_finish_submit_stream_ms.is_null()
        || out_accept_result_stream_ms.is_null()
        || out_read_result_packet_ms.is_null()
        || out_read_result_header_ms.is_null()
        || out_read_result_payload_ms.is_null()
        || out_quic_rtt_before_ms.is_null()
        || out_quic_rtt_after_accept_ms.is_null()
        || out_quic_rtt_after_read_ms.is_null()
        || out_quic_cwnd_before_bytes.is_null()
        || out_quic_cwnd_after_accept_bytes.is_null()
        || out_quic_cwnd_after_read_bytes.is_null()
        || out_quic_sent_packets_during_accept.is_null()
        || out_quic_lost_packets_during_accept.is_null()
        || out_quic_congestion_events_during_accept.is_null()
        || out_quic_sent_packets_total.is_null()
        || out_quic_lost_packets_total.is_null()
        || out_quic_congestion_events_total.is_null()
        || out_error.is_null()
    {
        return 2;
    }

    unsafe {
        *out_result_packet = ptr::null_mut();
        *out_result_packet_len = 0;
        *out_open_submit_stream_ms = 0.0;
        *out_write_submit_packet_ms = 0.0;
        *out_finish_submit_stream_ms = 0.0;
        *out_accept_result_stream_ms = 0.0;
        *out_read_result_packet_ms = 0.0;
        *out_read_result_header_ms = 0.0;
        *out_read_result_payload_ms = 0.0;
        *out_quic_rtt_before_ms = 0.0;
        *out_quic_rtt_after_accept_ms = 0.0;
        *out_quic_rtt_after_read_ms = 0.0;
        *out_quic_cwnd_before_bytes = 0;
        *out_quic_cwnd_after_accept_bytes = 0;
        *out_quic_cwnd_after_read_bytes = 0;
        *out_quic_sent_packets_during_accept = 0;
        *out_quic_lost_packets_during_accept = 0;
        *out_quic_congestion_events_during_accept = 0;
        *out_quic_sent_packets_total = 0;
        *out_quic_lost_packets_total = 0;
        *out_quic_congestion_events_total = 0;
        *out_error = ptr::null_mut();
    }

    let submit_packet =
        unsafe { std::slice::from_raw_parts(submit_packet, submit_packet_len as usize) };
    let result = (|| -> Result<NativeSubmitResult, String> {
        let command_tx = active_clients()
            .lock()
            .map_err(|_| "native client registry lock poisoned".to_string())?;
        let command_tx = command_tx
            .get(&handle)
            .ok_or_else(|| format!("unknown native client handle: {handle}"))?
            .command_tx
            .clone();

        let (response_tx, response_rx) = mpsc::channel();
        command_tx
            .send(NativeClientCommand::Submit {
                submit_packet: submit_packet.to_vec(),
                response_tx,
            })
            .map_err(|_| format!("native client worker terminated before submit: {handle}"))?;

        response_rx
            .recv()
            .map_err(|_| format!("native client worker dropped submit response: {handle}"))?
    })();

    match result {
        Ok(mut submit_result) => unsafe {
            *out_result_packet_len = submit_result.result_packet.len() as c_int;
            *out_result_packet = submit_result.result_packet.as_mut_ptr();
            *out_open_submit_stream_ms = submit_result.open_submit_stream_ms;
            *out_write_submit_packet_ms = submit_result.write_submit_packet_ms;
            *out_finish_submit_stream_ms = submit_result.finish_submit_stream_ms;
            *out_accept_result_stream_ms = submit_result.accept_result_stream_ms;
            *out_read_result_packet_ms = submit_result.read_result_packet_ms;
            *out_read_result_header_ms = submit_result.read_result_header_ms;
            *out_read_result_payload_ms = submit_result.read_result_payload_ms;
            *out_quic_rtt_before_ms = submit_result.quic_rtt_before_ms;
            *out_quic_rtt_after_accept_ms = submit_result.quic_rtt_after_accept_ms;
            *out_quic_rtt_after_read_ms = submit_result.quic_rtt_after_read_ms;
            *out_quic_cwnd_before_bytes = submit_result.quic_cwnd_before_bytes;
            *out_quic_cwnd_after_accept_bytes = submit_result.quic_cwnd_after_accept_bytes;
            *out_quic_cwnd_after_read_bytes = submit_result.quic_cwnd_after_read_bytes;
            *out_quic_sent_packets_during_accept = submit_result.quic_sent_packets_during_accept;
            *out_quic_lost_packets_during_accept = submit_result.quic_lost_packets_during_accept;
            *out_quic_congestion_events_during_accept =
                submit_result.quic_congestion_events_during_accept;
            *out_quic_sent_packets_total = submit_result.quic_sent_packets_total;
            *out_quic_lost_packets_total = submit_result.quic_lost_packets_total;
            *out_quic_congestion_events_total = submit_result.quic_congestion_events_total;
            std::mem::forget(submit_result.result_packet);
            0
        },
        Err(error_text) => unsafe {
            write_c_string(out_error, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_client_begin_submit(
    handle: u64,
    submit_packet: *const u8,
    submit_packet_len: c_int,
    out_error: *mut *mut c_char,
) -> c_int {
    if handle == 0 || submit_packet.is_null() || submit_packet_len <= 0 || out_error.is_null() {
        return 1;
    }

    unsafe {
        *out_error = ptr::null_mut();
    }

    let submit_packet = unsafe { std::slice::from_raw_parts(submit_packet, submit_packet_len as usize) };
    let result = (|| -> Result<(), String> {
        ensure_message_type(submit_packet, MSG_FRAME_SUBMIT, "FRAME_SUBMIT")?;

        let command_tx = active_clients()
            .lock()
            .map_err(|_| "native client registry lock poisoned".to_string())?;
        let command_tx = command_tx
            .get(&handle)
            .ok_or_else(|| format!("unknown native client handle: {handle}"))?
            .command_tx
            .clone();

        let (response_tx, response_rx) = mpsc::channel();
        command_tx
            .send(NativeClientCommand::BeginSubmit {
                submit_packet: submit_packet.to_vec(),
                response_tx,
            })
            .map_err(|_| format!("native client worker terminated before begin-submit: {handle}"))?;

        response_rx
            .recv()
            .map_err(|_| format!("native client worker dropped begin-submit response: {handle}"))?
    })();

    match result {
        Ok(()) => 0,
        Err(error_text) => unsafe {
            write_c_string(out_error, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_client_ping(
    handle: u64,
    ping_packet: *const u8,
    ping_packet_len: c_int,
    out_pong_packet: *mut *mut u8,
    out_pong_packet_len: *mut c_int,
    out_error: *mut *mut c_char,
) -> c_int {
    if handle == 0
        || ping_packet.is_null()
        || ping_packet_len <= 0
        || out_pong_packet.is_null()
        || out_pong_packet_len.is_null()
        || out_error.is_null()
    {
        return 2;
    }

    unsafe {
        *out_pong_packet = ptr::null_mut();
        *out_pong_packet_len = 0;
        *out_error = ptr::null_mut();
    }

    let ping_packet = unsafe { std::slice::from_raw_parts(ping_packet, ping_packet_len as usize) };
    let result = (|| -> Result<Vec<u8>, String> {
        let command_tx = active_clients()
            .lock()
            .map_err(|_| "native client registry lock poisoned".to_string())?;
        let command_tx = command_tx
            .get(&handle)
            .ok_or_else(|| format!("unknown native client handle: {handle}"))?
            .command_tx
            .clone();

        let (response_tx, response_rx) = mpsc::channel();
        command_tx
            .send(NativeClientCommand::Ping {
                ping_packet: ping_packet.to_vec(),
                response_tx,
            })
            .map_err(|_| format!("native client worker terminated before ping: {handle}"))?;

        response_rx
            .recv()
            .map_err(|_| format!("native client worker dropped ping response: {handle}"))?
    })();

    match result {
        Ok(mut packet) => unsafe {
            *out_pong_packet_len = packet.len() as c_int;
            *out_pong_packet = packet.as_mut_ptr();
            std::mem::forget(packet);
            0
        },
        Err(error_text) => unsafe {
            write_c_string(out_error, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_client_receive_result(
    handle: u64,
    out_result_packet: *mut *mut u8,
    out_result_packet_len: *mut c_int,
    out_error: *mut *mut c_char,
) -> c_int {
    if handle == 0 || out_result_packet.is_null() || out_result_packet_len.is_null() || out_error.is_null() {
        return 1;
    }

    unsafe {
        *out_result_packet = ptr::null_mut();
        *out_result_packet_len = 0;
        *out_error = ptr::null_mut();
    }

    let result = (|| -> Result<Vec<u8>, String> {
        let command_tx = active_clients()
            .lock()
            .map_err(|_| "native client registry lock poisoned".to_string())?;
        let command_tx = command_tx
            .get(&handle)
            .ok_or_else(|| format!("unknown native client handle: {handle}"))?
            .command_tx
            .clone();

        let (response_tx, response_rx) = mpsc::channel();
        command_tx
            .send(NativeClientCommand::ReceiveResult { response_tx })
            .map_err(|_| format!("native client worker terminated before receive-result: {handle}"))?;

        response_rx
            .recv()
            .map_err(|_| format!("native client worker dropped receive-result response: {handle}"))?
    })();

    match result {
        Ok(mut result_packet) => unsafe {
            *out_result_packet_len = result_packet.len() as c_int;
            *out_result_packet = result_packet.as_mut_ptr();
            std::mem::forget(result_packet);
            0
        },
        Err(error_text) => unsafe {
            write_c_string(out_error, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_client_receive_session_packet(
    handle: u64,
    out_packet: *mut *mut u8,
    out_packet_len: *mut c_int,
    out_error: *mut *mut c_char,
) -> c_int {
    if handle == 0 || out_packet.is_null() || out_packet_len.is_null() || out_error.is_null() {
        return 1;
    }

    unsafe {
        *out_packet = ptr::null_mut();
        *out_packet_len = 0;
        *out_error = ptr::null_mut();
    }

    let result = (|| -> Result<Vec<u8>, String> {
        let command_tx = active_clients()
            .lock()
            .map_err(|_| "native client registry lock poisoned".to_string())?;
        let command_tx = command_tx
            .get(&handle)
            .ok_or_else(|| format!("unknown native client handle: {handle}"))?
            .command_tx
            .clone();

        let (response_tx, response_rx) = mpsc::channel();
        command_tx
            .send(NativeClientCommand::ReceiveSessionPacket { response_tx })
            .map_err(|_| format!("native client worker terminated before receive-session-packet: {handle}"))?;

        response_rx
            .recv()
            .map_err(|_| format!("native client worker dropped receive-session-packet response: {handle}"))?
    })();

    match result {
        Ok(mut packet) => unsafe {
            *out_packet_len = packet.len() as c_int;
            *out_packet = packet.as_mut_ptr();
            std::mem::forget(packet);
            0
        },
        Err(error_text) => unsafe {
            write_c_string(out_error, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_client_cancel(
    handle: u64,
    cancel_packet: *const u8,
    cancel_packet_len: c_int,
    out_error: *mut *mut c_char,
) -> c_int {
    if handle == 0 || cancel_packet.is_null() || cancel_packet_len <= 0 || out_error.is_null() {
        return 2;
    }

    unsafe {
        *out_error = ptr::null_mut();
    }

    let cancel_packet =
        unsafe { std::slice::from_raw_parts(cancel_packet, cancel_packet_len as usize) };
    let result = (|| -> Result<(), String> {
        let command_tx = active_clients()
            .lock()
            .map_err(|_| "native client registry lock poisoned".to_string())?;
        let command_tx = command_tx
            .get(&handle)
            .ok_or_else(|| format!("unknown native client handle: {handle}"))?
            .command_tx
            .clone();

        let (response_tx, response_rx) = mpsc::channel();
        command_tx
            .send(NativeClientCommand::Cancel {
                cancel_packet: cancel_packet.to_vec(),
                response_tx,
            })
            .map_err(|_| format!("native client worker terminated before cancel: {handle}"))?;

        response_rx
            .recv()
            .map_err(|_| format!("native client worker dropped cancel response: {handle}"))?
    })();

    match result {
        Ok(()) => 0,
        Err(error_text) => unsafe {
            write_c_string(out_error, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_client_close(handle: u64, out_error: *mut *mut c_char) -> c_int {
    if out_error.is_null() {
        return 2;
    }

    unsafe {
        *out_error = ptr::null_mut();
    }

    let result = (|| -> Result<(), String> {
        let client = active_clients()
            .lock()
            .map_err(|_| "native client registry lock poisoned".to_string())?
            .remove(&handle)
            .ok_or_else(|| format!("unknown native client handle: {handle}"))?;

        let (response_tx, response_rx) = mpsc::channel();
        client
            .command_tx
            .send(NativeClientCommand::Close { response_tx })
            .map_err(|_| format!("native client worker terminated before close: {handle}"))?;

        let close_result = response_rx
            .recv()
            .map_err(|_| format!("native client worker dropped close response: {handle}"))?;
        let join_result = client
            .worker_thread
            .join()
            .map_err(|_| format!("native client worker panicked during close: {handle}"));

        close_result?;
        join_result
    })();

    match result {
        Ok(()) => 0,
        Err(error_text) => unsafe {
            write_c_string(out_error, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_smoke_run_json(
    host: *const c_char,
    port: u16,
    tls_server_name: *const c_char,
    requested_session_id: u32,
    frame_id: u32,
    out_json: *mut *mut c_char,
) -> c_int {
    if out_json.is_null() {
        return 2;
    }

    let result = run_smoke_ffi(host, port, tls_server_name, requested_session_id, frame_id);
    match result {
        Ok(json_text) => unsafe {
            write_c_string(out_json, &json_text);
            0
        },
        Err(error_text) => unsafe {
            write_c_string(out_json, &error_text);
            1
        },
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_string_free(value: *mut c_char) {
    if value.is_null() {
        return;
    }

    unsafe {
        let _ = CString::from_raw(value);
    }
}

#[no_mangle]
pub extern "C" fn nnrp_quic_buffer_free(value: *mut u8, length: c_int) {
    if value.is_null() || length <= 0 {
        return;
    }

    unsafe {
        let _ = Vec::from_raw_parts(value, length as usize, length as usize);
    }
}

fn run_smoke_ffi(
    host: *const c_char,
    port: u16,
    tls_server_name: *const c_char,
    requested_session_id: u32,
    frame_id: u32,
) -> Result<String, String> {
    let host = read_required_c_string(host, "host")?;
    let tls_server_name = read_required_c_string(tls_server_name, "tls_server_name")?;
    let runtime = build_runtime()?;

    run_smoke(
        &host,
        port,
        &tls_server_name,
        requested_session_id,
        frame_id,
        runtime,
    )
}

fn run_smoke(
    host: &str,
    port: u16,
    tls_server_name: &str,
    requested_session_id: u32,
    frame_id: u32,
    runtime: Runtime,
) -> Result<String, String> {
    let client = open_native_client(
        runtime,
        host,
        port,
        tls_server_name,
        "",
        requested_session_id,
        CURRENT_WIRE_FORMAT,
    )?;
    let negotiated_session_id = client.negotiated_session_id;
    let active_model_name = client.active_model_name.clone();
    let NativeClient {
        runtime,
        endpoint,
        connection,
        mut control_send,
        control_recv: _,
        negotiated_session_id: _,
        negotiated_wire_format: _,
        active_model_name: _,
    } = client;

    let submit_packet = build_runtime_smoke_submit_packet(negotiated_session_id, frame_id)?;
    let result_packet = runtime.block_on(async {
        let mut submit_send = connection
            .open_uni()
            .await
            .map_err(|error| format!("failed to open submit stream: {error}"))?;
        write_packet(&mut submit_send, &submit_packet)
            .await
            .map_err(|error| format!("failed to send FRAME_SUBMIT: {error}"))?;
        submit_send
            .finish()
            .map_err(|error| format!("failed to finish submit stream: {error}"))?;

        let mut result_recv = connection
            .accept_uni()
            .await
            .map_err(|error| format!("failed to accept result stream: {error}"))?;
        read_packet(&mut result_recv)
            .await
            .map_err(|error| format!("failed to receive RESULT_PUSH: {error}"))
    })?;
    ensure_message_type(&result_packet, MSG_RESULT_PUSH, "RESULT_PUSH")?;
    let result_tile_count = parse_result_tile_count(&result_packet)?;

    let close_packet = build_close_packet(negotiated_session_id, "rust-native-smoke");
    runtime.block_on(async {
        write_packet(&mut control_send, &close_packet)
            .await
            .map_err(|error| format!("failed to send CLOSE: {error}"))
    })?;

    let payload = json!({
        "transport": "nnrp-rust-native",
        "host": host,
        "port": port,
        "tls_server_name": tls_server_name,
        "active_model_name": active_model_name,
        "requested_session_id": requested_session_id,
        "negotiated_session_id": negotiated_session_id,
        "frame_id": frame_id,
        "submit_tile_count": 2,
        "result_tile_count": result_tile_count
    });

    connection.close(VarInt::from_u32(0), b"done");
    drop(endpoint);
    Ok(payload.to_string())
}

fn open_native_client(
    runtime: Runtime,
    host: &str,
    port: u16,
    tls_server_name: &str,
    requested_model: &str,
    requested_session_id: u32,
    requested_wire_format: u8,
) -> Result<NativeClient, String> {
    let remote = resolve_remote_endpoint(host, port)?;
    let client_config = build_client_config(requested_wire_format)?;
    let bind_address: SocketAddr = "0.0.0.0:0"
        .parse()
        .map_err(|error| format!("failed to parse local bind address: {error}"))?;

    let (
        endpoint,
        connection,
        control_send,
        control_recv,
        negotiated_session_id,
        negotiated_wire_format,
        active_model_name,
    ) = runtime.block_on(async {
        let mut endpoint = Endpoint::client(bind_address)
            .map_err(|error| format!("failed to create QUIC endpoint: {error}"))?;
        endpoint.set_default_client_config(client_config);

        let connection = timeout(
            Duration::from_secs(DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS),
            endpoint
                .connect(remote, tls_server_name)
                .map_err(|error| format!("failed to start QUIC connect: {error}"))?,
        )
        .await
        .map_err(|_| {
            format!(
                "timed out completing QUIC handshake after {} seconds",
                DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to complete QUIC handshake: {error}"))?;

        let (mut control_send, mut control_recv) = timeout(
            Duration::from_secs(DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS),
            connection.open_bi(),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out opening QUIC control stream after {} seconds",
                DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to open control stream: {error}"))?;

        let hello_packet = build_client_hello_packet(
            requested_wire_format,
            requested_session_id,
            requested_model.as_bytes(),
        )?;
        timeout(
            Duration::from_secs(DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS),
            write_packet(&mut control_send, &hello_packet),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out sending CLIENT_HELLO after {} seconds",
                DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to send CLIENT_HELLO: {error}"))?;

        let ack_packet = timeout(
            Duration::from_secs(DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS),
            read_packet(&mut control_recv),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out receiving SERVER_HELLO_ACK after {} seconds",
                DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to receive SERVER_HELLO_ACK: {error}"))?;
        ensure_message_type(&ack_packet, MSG_SERVER_HELLO_ACK, "SERVER_HELLO_ACK")?;

        let negotiated_session_id = parse_server_hello_ack_session_id(&ack_packet)?;
        let negotiated_wire_format = parse_server_hello_ack_selected_wire_format(&ack_packet)?;
        if negotiated_wire_format != requested_wire_format {
            return Err(format!(
                "SERVER_HELLO_ACK selected wire format {} but requested wire format was {}",
                negotiated_wire_format, requested_wire_format
            ));
        }

        let active_model_name = parse_server_hello_ack_active_model_name(&ack_packet)
            .unwrap_or_else(|| requested_model.to_string());

        Ok::<_, String>((
            endpoint,
            connection,
            control_send,
            control_recv,
            negotiated_session_id,
            negotiated_wire_format,
            active_model_name,
        ))
    })?;

    Ok(NativeClient {
        runtime,
        endpoint,
        connection,
        control_send,
        control_recv,
        negotiated_session_id,
        negotiated_wire_format,
        active_model_name,
    })
}

fn spawn_native_client_worker(
    host: &str,
    port: u16,
    tls_server_name: &str,
    requested_model: &str,
    requested_session_id: u32,
    requested_wire_format: u8,
) -> Result<(NativeClientWorker, u32, u8, String), String> {
    let (ready_tx, ready_rx) = mpsc::channel();
    let (command_tx, command_rx) = mpsc::channel();
    let host = host.to_string();
    let tls_server_name = tls_server_name.to_string();
    let requested_model = requested_model.to_string();

    let worker_thread = thread::spawn(move || {
        let open_result = (|| -> Result<NativeClient, String> {
            let runtime = build_runtime()?;
            open_native_client(
                runtime,
                &host,
                port,
                &tls_server_name,
                &requested_model,
                requested_session_id,
                requested_wire_format,
            )
        })();

        match open_result {
            Ok(client) => {
                let _ = ready_tx.send(Ok((
                    client.negotiated_session_id,
                    client.negotiated_wire_format,
                    client.active_model_name.clone(),
                )));
                run_native_client_worker_loop(client, command_rx);
            }
            Err(error_text) => {
                let _ = ready_tx.send(Err(error_text));
            }
        }
    });

    let (negotiated_session_id, negotiated_wire_format, active_model_name) =
        ready_rx
            .recv()
            .map_err(|_| "native client worker terminated before open completed".to_string())??;

    Ok((
        NativeClientWorker {
            command_tx,
            worker_thread,
        },
        negotiated_session_id,
        negotiated_wire_format,
        active_model_name,
    ))
}

fn probe_native_quic(
    host: &str,
    port: u16,
    tls_server_name: &str,
    probe_packet: &[u8],
    requested_wire_format: u8,
) -> Result<Vec<u8>, String> {
    let runtime = build_runtime()?;
    let remote = resolve_remote_endpoint(host, port)?;
    let client_config = build_client_config(requested_wire_format)?;
    let bind_address: SocketAddr = "0.0.0.0:0"
        .parse()
        .map_err(|error| format!("failed to parse local bind address: {error}"))?;

    runtime.block_on(async {
        let mut endpoint = Endpoint::client(bind_address)
            .map_err(|error| format!("failed to create QUIC endpoint: {error}"))?;
        endpoint.set_default_client_config(client_config);

        let connection = timeout(
            Duration::from_secs(DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS),
            endpoint
                .connect(remote, tls_server_name)
                .map_err(|error| format!("failed to start QUIC connect: {error}"))?,
        )
        .await
        .map_err(|_| {
            format!(
                "timed out completing QUIC handshake after {} seconds",
                DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to complete QUIC handshake: {error}"))?;

        let (mut control_send, mut control_recv) = timeout(
            Duration::from_secs(DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS),
            connection.open_bi(),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out opening QUIC control stream after {} seconds",
                DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to open control stream: {error}"))?;

        timeout(
            Duration::from_secs(DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS),
            write_packet(&mut control_send, probe_packet),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out sending TRANSPORT_PROBE after {} seconds",
                DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to send TRANSPORT_PROBE: {error}"))?;

        let response_packet = timeout(
            Duration::from_secs(DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS),
            read_packet(&mut control_recv),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out receiving TRANSPORT_PROBE_ACK after {} seconds",
                DEFAULT_CONNECT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to receive TRANSPORT_PROBE_ACK: {error}"))?;
        ensure_message_type(
            &response_packet,
            MSG_TRANSPORT_PROBE_ACK,
            "TRANSPORT_PROBE_ACK",
        )?;

        connection.close(VarInt::from_u32(0), b"probe_done");
        drop(control_recv);
        drop(control_send);
        drop(endpoint);
        Ok(response_packet)
    })
}

fn run_native_client_worker_loop(
    mut client: NativeClient,
    command_rx: mpsc::Receiver<NativeClientCommand>,
) {
    while let Ok(command) = command_rx.recv() {
        match command {
            NativeClientCommand::BeginSubmit {
                submit_packet,
                response_tx,
            } => {
                let _ = response_tx.send(begin_submit_native_client(&mut client, &submit_packet));
            }
            NativeClientCommand::ReceiveResult { response_tx } => {
                let _ = response_tx.send(receive_result_native_client(&mut client));
            }
            NativeClientCommand::ReceiveSessionPacket { response_tx } => {
                let _ = response_tx.send(receive_session_packet_native_client(&mut client));
            }
            NativeClientCommand::Submit {
                submit_packet,
                response_tx,
            } => {
                let _ = response_tx.send(submit_native_client(&mut client, &submit_packet));
            }
            NativeClientCommand::Ping {
                ping_packet,
                response_tx,
            } => {
                let _ = response_tx.send(ping_native_client(&mut client, &ping_packet));
            }
            NativeClientCommand::Cancel {
                cancel_packet,
                response_tx,
            } => {
                let _ = response_tx.send(cancel_native_client(&mut client, &cancel_packet));
            }
            NativeClientCommand::Close { response_tx } => {
                let _ = response_tx.send(close_native_client(client));
                return;
            }
        }
    }

    let _ = close_native_client(client);
}

fn begin_submit_native_client(client: &mut NativeClient, submit_packet: &[u8]) -> Result<(), String> {
    client.runtime.block_on(async {
        ensure_message_type(submit_packet, MSG_FRAME_SUBMIT, "FRAME_SUBMIT")?;

        let mut submit_send = timeout(
            Duration::from_secs(DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS),
            client.connection.open_uni(),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out after {}s waiting to open submit stream",
                DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to open submit stream: {error}"))?;

        timeout(
            Duration::from_secs(DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS),
            write_packet(&mut submit_send, submit_packet),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out after {}s while writing FRAME_SUBMIT bytes",
                DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to send FRAME_SUBMIT: {error}"))?;

        submit_send
            .finish()
            .map_err(|error| format!("failed to finish submit stream: {error}"))?;

        Ok(())
    })
}

fn receive_result_native_client(client: &mut NativeClient) -> Result<Vec<u8>, String> {
    let result_packet = receive_session_packet_native_client(client)?;
    ensure_message_type(&result_packet, MSG_RESULT_PUSH, "RESULT_PUSH")?;
    Ok(result_packet)
}

fn receive_session_packet_native_client(client: &mut NativeClient) -> Result<Vec<u8>, String> {
    client.runtime.block_on(async {
        let mut result_recv = timeout(
            Duration::from_secs(DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS),
            client.connection.accept_uni(),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out after {}s waiting for result stream",
                DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to accept result stream: {error}"))?;

        let result_packet = timeout(
            Duration::from_secs(DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS),
            read_packet(&mut result_recv),
        )
        .await
        .map_err(|_| {
            format!(
                    "timed out after {}s while reading session packet payload",
                DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS
            )
        })?
            .map_err(|error| format!("failed to receive session packet: {error}"))?;

        ensure_runtime_session_message_type(&result_packet)?;
        Ok(result_packet)
    })
}

fn submit_native_client(
    client: &mut NativeClient,
    submit_packet: &[u8],
) -> Result<NativeSubmitResult, String> {
    client.runtime.block_on(async {
        let quic_before = capture_quic_path_snapshot(&client.connection);

        let open_submit_stream_started_at = Instant::now();
        let mut submit_send = timeout(
            Duration::from_secs(DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS),
            client.connection.open_uni(),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out after {}s waiting to open submit stream",
                DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to open submit stream: {error}"))?;
        let open_submit_stream_ms = open_submit_stream_started_at.elapsed().as_secs_f64() * 1000.0;

        let write_submit_packet_started_at = Instant::now();
        timeout(
            Duration::from_secs(DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS),
            write_packet(&mut submit_send, submit_packet),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out after {}s while writing FRAME_SUBMIT bytes",
                DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to send FRAME_SUBMIT: {error}"))?;
        let write_submit_packet_ms =
            write_submit_packet_started_at.elapsed().as_secs_f64() * 1000.0;

        let finish_submit_stream_started_at = Instant::now();
        submit_send
            .finish()
            .map_err(|error| format!("failed to finish submit stream: {error}"))?;
        let finish_submit_stream_ms =
            finish_submit_stream_started_at.elapsed().as_secs_f64() * 1000.0;

        let accept_result_stream_started_at = Instant::now();
        let mut result_recv = timeout(
            Duration::from_secs(DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS),
            client.connection.accept_uni(),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out after {}s waiting for result stream",
                DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to accept result stream: {error}"))?;
        let accept_result_stream_ms =
            accept_result_stream_started_at.elapsed().as_secs_f64() * 1000.0;
        let quic_after_accept = capture_quic_path_snapshot(&client.connection);

        let (result_packet, read_result_header_ms, read_result_payload_ms) = timeout(
            Duration::from_secs(DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS),
            read_packet_with_timings(&mut result_recv),
        )
        .await
        .map_err(|_| {
            format!(
                "timed out after {}s while reading RESULT_PUSH payload",
                DEFAULT_SUBMIT_STAGE_TIMEOUT_SECONDS
            )
        })?
        .map_err(|error| format!("failed to receive RESULT_PUSH: {error}"))?;
        let read_result_packet_ms = read_result_header_ms + read_result_payload_ms;
        let quic_after_read = capture_quic_path_snapshot(&client.connection);
        ensure_message_type(&result_packet, MSG_RESULT_PUSH, "RESULT_PUSH")?;
        Ok(NativeSubmitResult {
            result_packet,
            open_submit_stream_ms,
            write_submit_packet_ms,
            finish_submit_stream_ms,
            accept_result_stream_ms,
            read_result_packet_ms,
            read_result_header_ms,
            read_result_payload_ms,
            quic_rtt_before_ms: quic_before.rtt_ms,
            quic_rtt_after_accept_ms: quic_after_accept.rtt_ms,
            quic_rtt_after_read_ms: quic_after_read.rtt_ms,
            quic_cwnd_before_bytes: quic_before.cwnd_bytes,
            quic_cwnd_after_accept_bytes: quic_after_accept.cwnd_bytes,
            quic_cwnd_after_read_bytes: quic_after_read.cwnd_bytes,
            quic_sent_packets_during_accept: quic_after_accept
                .sent_packets
                .saturating_sub(quic_before.sent_packets),
            quic_lost_packets_during_accept: quic_after_accept
                .lost_packets
                .saturating_sub(quic_before.lost_packets),
            quic_congestion_events_during_accept: quic_after_accept
                .congestion_events
                .saturating_sub(quic_before.congestion_events),
            quic_sent_packets_total: quic_after_read
                .sent_packets
                .saturating_sub(quic_before.sent_packets),
            quic_lost_packets_total: quic_after_read
                .lost_packets
                .saturating_sub(quic_before.lost_packets),
            quic_congestion_events_total: quic_after_read
                .congestion_events
                .saturating_sub(quic_before.congestion_events),
        })
    })
}

fn capture_quic_path_snapshot(connection: &Connection) -> NativeQuicPathSnapshot {
    let path = connection.stats().path;
    NativeQuicPathSnapshot {
        rtt_ms: path.rtt.as_secs_f64() * 1000.0,
        cwnd_bytes: path.cwnd,
        sent_packets: path.sent_packets,
        lost_packets: path.lost_packets,
        congestion_events: path.congestion_events,
    }
}

fn ping_native_client(client: &mut NativeClient, ping_packet: &[u8]) -> Result<Vec<u8>, String> {
    client.runtime.block_on(async {
        write_packet(&mut client.control_send, ping_packet)
            .await
            .map_err(|error| format!("failed to send PING: {error}"))?;

        let pong_packet = read_packet(&mut client.control_recv)
            .await
            .map_err(|error| format!("failed to receive PONG: {error}"))?;
        ensure_message_type(&pong_packet, MSG_PONG, "PONG")?;
        Ok(pong_packet)
    })
}

fn cancel_native_client(client: &mut NativeClient, cancel_packet: &[u8]) -> Result<(), String> {
    client.runtime.block_on(async {
        write_packet(&mut client.control_send, cancel_packet)
            .await
            .map_err(|error| format!("failed to send FRAME_CANCEL: {error}"))
    })
}

fn close_native_client(mut client: NativeClient) -> Result<(), String> {
    let close_packet = build_close_packet(client.negotiated_session_id, "native-bridge-client");
    let _ = client.runtime.block_on(async {
        write_packet(&mut client.control_send, &close_packet)
            .await
            .map_err(|error| format!("failed to send CLOSE: {error}"))
    });
    client
        .connection
        .close(VarInt::from_u32(0), b"client_close");
    drop(client.control_recv);
    drop(client.control_send);
    drop(client.endpoint);
    Ok(())
}

fn resolve_remote_endpoint(host: &str, port: u16) -> Result<SocketAddr, String> {
    let target = format!("{host}:{port}");
    target
        .to_socket_addrs()
        .map_err(|error| format!("failed to resolve remote endpoint {target}: {error}"))?
        .next()
        .ok_or_else(|| format!("no socket address resolved for {target}"))
}

fn build_client_hello_packet(
    requested_wire_format: u8,
    requested_session_id: u32,
    auth_body: &[u8],
) -> Result<Vec<u8>, String> {
    if requested_wire_format != CURRENT_WIRE_FORMAT {
        return Err(format!(
            "requested_wire_format must be {}",
            CURRENT_WIRE_FORMAT
        ));
    }

    let mut metadata = Vec::with_capacity(16 * 4);
    for value in [
        1u32,
        1u32,
        0x0001u32,
        0x0001u32,
        0x0001u32,
        0x0001u32,
        0x0003u32,
        0x0001u32,
        0x003Fu32,
        0x0001u32,
        0x0001u32,
        256u32,
        16u32,
        1024u32 * 1024u32,
        24000u32,
        100u32,
        2u32,
        0x000Fu32,
        requested_session_id,
        auth_body.len() as u32,
        0u32,
    ] {
        metadata.extend_from_slice(&value.to_le_bytes());
    }

    Ok(build_packet(
        requested_wire_format,
        MSG_CLIENT_HELLO,
        FLAG_ACK_REQUIRED,
        0,
        0,
        0,
        0,
        0,
        &metadata,
        auth_body,
    ))
}

fn build_close_packet(session_id: u32, reason: &str) -> Vec<u8> {
    build_packet(
        CURRENT_WIRE_FORMAT,
        MSG_CLOSE,
        0,
        session_id,
        0,
        0,
        0,
        0,
        &[],
        reason.as_bytes(),
    )
}

fn build_runtime_smoke_submit_packet(session_id: u32, frame_id: u32) -> Result<Vec<u8>, String> {
    let mut metadata = Vec::with_capacity(52);
    for value in [64u16, 64u16, 32u16, 32u16, 3u16, 1u16] {
        metadata.extend_from_slice(&value.to_le_bytes());
    }
    metadata.push(0u8);
    metadata.push(1u8);
    metadata.push(1u8);
    metadata.push(0u8);
    metadata.extend_from_slice(&16u16.to_le_bytes());
    metadata.extend_from_slice(&0u16.to_le_bytes());
    metadata.extend_from_slice(&0u32.to_le_bytes());
    metadata.extend_from_slice(&0u32.to_le_bytes());
    metadata.extend_from_slice(&22u32.to_le_bytes());
    metadata.extend_from_slice(&6u32.to_le_bytes());
    metadata.extend_from_slice(&0u64.to_le_bytes());
    metadata.extend_from_slice(&0u64.to_le_bytes());

    let mut body = Vec::with_capacity(3144);

    body.extend_from_slice(b"NRCM");
    body.extend_from_slice(&1u16.to_le_bytes());
    body.extend_from_slice(&0u16.to_le_bytes());
    body.extend_from_slice(&0u16.to_le_bytes());
    body.extend_from_slice(&0u16.to_le_bytes());
    body.extend_from_slice(&0u16.to_le_bytes());
    body.extend_from_slice(&0f32.to_le_bytes());
    body.extend_from_slice(&0f32.to_le_bytes());

    for value in [0u16, 1u16, 2u16] {
        body.extend_from_slice(&value.to_le_bytes());
    }

    body.extend_from_slice(&5u16.to_le_bytes());
    body.push(0u8);
    body.push(5u8);
    body.push(0u8);
    body.push(0u8);
    body.extend_from_slice(&0u16.to_le_bytes());
    body.extend_from_slice(&0u32.to_le_bytes());
    body.extend_from_slice(&0u32.to_le_bytes());
    body.extend_from_slice(&12u32.to_le_bytes());
    body.extend_from_slice(&3072u32.to_le_bytes());
    body.extend_from_slice(&0u32.to_le_bytes());
    body.extend_from_slice(&0u32.to_le_bytes());

    for _ in 0..3 {
        body.extend_from_slice(&1024u32.to_le_bytes());
    }

    body.extend(std::iter::repeat(7u8).take(1024));
    body.extend(std::iter::repeat(9u8).take(1024));
    body.extend(std::iter::repeat(11u8).take(1024));

    Ok(build_packet(
        CURRENT_WIRE_FORMAT,
        MSG_FRAME_SUBMIT,
        0,
        session_id,
        frame_id,
        0,
        0,
        0,
        &metadata,
        &body,
    ))
}

fn build_packet(
    wire_format: u8,
    msg_type: u8,
    flags: u32,
    session_id: u32,
    frame_id: u32,
    view_id: u16,
    route_id: u16,
    trace_id: u64,
    metadata: &[u8],
    body: &[u8],
) -> Vec<u8> {
    let mut packet = Vec::with_capacity(HEADER_LENGTH + metadata.len() + body.len());
    packet.extend_from_slice(b"NNRP");
    packet.push(VERSION_MAJOR);
    packet.push(wire_format);
    packet.push(msg_type);
    packet.push(HEADER_LENGTH as u8);
    packet.extend_from_slice(&flags.to_le_bytes());
    packet.extend_from_slice(&(metadata.len() as u32).to_le_bytes());
    packet.extend_from_slice(&(body.len() as u32).to_le_bytes());
    packet.extend_from_slice(&session_id.to_le_bytes());
    packet.extend_from_slice(&frame_id.to_le_bytes());
    packet.extend_from_slice(&view_id.to_le_bytes());
    packet.extend_from_slice(&route_id.to_le_bytes());
    packet.extend_from_slice(&trace_id.to_le_bytes());
    packet.extend_from_slice(metadata);
    packet.extend_from_slice(body);
    packet
}

async fn write_packet(stream: &mut SendStream, packet: &[u8]) -> Result<(), WriteError> {
    stream.write_all(packet).await
}

async fn read_packet(stream: &mut RecvStream) -> Result<Vec<u8>, String> {
    let (packet, _, _) = read_packet_with_timings(stream).await?;
    Ok(packet)
}

async fn read_packet_with_timings(stream: &mut RecvStream) -> Result<(Vec<u8>, f64, f64), String> {
    let mut header = [0u8; HEADER_LENGTH];
    let read_header_started_at = Instant::now();
    stream
        .read_exact(&mut header)
        .await
        .map_err(|error| format!("failed to read packet header: {error}"))?;
    let read_result_header_ms = read_header_started_at.elapsed().as_secs_f64() * 1000.0;
    ensure_header_magic(&header)?;

    let meta_len = u32::from_le_bytes(header[12..16].try_into().unwrap()) as usize;
    let body_len = u32::from_le_bytes(header[16..20].try_into().unwrap()) as usize;
    let mut remainder = vec![0u8; meta_len + body_len];
    let read_payload_started_at = Instant::now();
    stream
        .read_exact(&mut remainder)
        .await
        .map_err(|error| format!("failed to read packet payload: {error}"))?;
    let read_result_payload_ms = read_payload_started_at.elapsed().as_secs_f64() * 1000.0;

    let mut packet = Vec::with_capacity(HEADER_LENGTH + remainder.len());
    packet.extend_from_slice(&header);
    packet.extend_from_slice(&remainder);
    Ok((packet, read_result_header_ms, read_result_payload_ms))
}

fn ensure_header_magic(header: &[u8; HEADER_LENGTH]) -> Result<(), String> {
    if &header[0..4] != b"NNRP" {
        return Err(format!(
            "unexpected packet magic: {:02x}{:02x}{:02x}{:02x}",
            header[0], header[1], header[2], header[3]
        ));
    }
    if header[7] != HEADER_LENGTH as u8 {
        return Err(format!("unexpected header_len: {}", header[7]));
    }
    Ok(())
}

fn ensure_message_type(packet: &[u8], expected: u8, label: &str) -> Result<(), String> {
    let actual = packet.get(6).copied().unwrap_or_default();
    if actual != expected {
        return Err(format!(
            "expected {label} message type 0x{expected:02x}, got 0x{actual:02x}"
        ));
    }
    Ok(())
}

fn ensure_runtime_session_message_type(packet: &[u8]) -> Result<(), String> {
    let actual = packet.get(6).copied().unwrap_or_default();
    if matches!(actual, MSG_RESULT_PUSH | MSG_FLOW_UPDATE | MSG_RESULT_HINT) {
        return Ok(());
    }

    Err(format!(
        "expected session message type RESULT_PUSH/FLOW_UPDATE/RESULT_HINT, got 0x{actual:02x}"
    ))
}

fn parse_server_hello_ack_session_id(packet: &[u8]) -> Result<u32, String> {
    let meta_len = u32::from_le_bytes(packet[12..16].try_into().unwrap()) as usize;
    let metadata = &packet[HEADER_LENGTH..HEADER_LENGTH + meta_len];
    if metadata.len() < 8 {
        return Err("SERVER_HELLO_ACK metadata is shorter than 8 bytes".to_string());
    }
    Ok(u32::from_le_bytes(metadata[4..8].try_into().unwrap()))
}

fn parse_server_hello_ack_selected_wire_format(packet: &[u8]) -> Result<u8, String> {
    let meta_len = u32::from_le_bytes(packet[12..16].try_into().unwrap()) as usize;
    let metadata = &packet[HEADER_LENGTH..HEADER_LENGTH + meta_len];
    if metadata.len() < 2 {
        return Err("SERVER_HELLO_ACK metadata is shorter than 2 bytes".to_string());
    }

    Ok(metadata[1])
}

fn parse_server_hello_ack_active_model_name(packet: &[u8]) -> Option<String> {
    let body = packet_body(packet)?;
    if body.is_empty() {
        return None;
    }

    let text = std::str::from_utf8(body).ok()?.trim();
    if text.is_empty() {
        None
    } else {
        Some(text.to_string())
    }
}

fn parse_result_tile_count(packet: &[u8]) -> Result<u16, String> {
    let meta_len = u32::from_le_bytes(packet[12..16].try_into().unwrap()) as usize;
    let metadata = &packet[HEADER_LENGTH..HEADER_LENGTH + meta_len];
    if metadata.len() < 8 {
        return Err("RESULT_PUSH metadata is shorter than 8 bytes".to_string());
    }
    Ok(u16::from_le_bytes(metadata[6..8].try_into().unwrap()))
}

fn packet_body(packet: &[u8]) -> Option<&[u8]> {
    if packet.len() < HEADER_LENGTH {
        return None;
    }

    let meta_len = u32::from_le_bytes(packet[12..16].try_into().ok()?) as usize;
    let body_len = u32::from_le_bytes(packet[16..20].try_into().ok()?) as usize;
    let body_offset = HEADER_LENGTH + meta_len;
    let body_end = body_offset.checked_add(body_len)?;
    packet.get(body_offset..body_end)
}

fn read_required_c_string(value: *const c_char, field_name: &str) -> Result<String, String> {
    if value.is_null() {
        return Err(format!("{field_name} pointer is null"));
    }

    let text = unsafe { CStr::from_ptr(value) }
        .to_str()
        .map_err(|error| format!("{field_name} is not valid UTF-8: {error}"))?
        .trim()
        .to_string();
    if text.is_empty() {
        return Err(format!("{field_name} must not be empty"));
    }
    Ok(text)
}

unsafe fn write_c_string(out_json: *mut *mut c_char, value: &str) {
    let sanitized = value.replace('\0', " ");
    let c_string = CString::new(sanitized).expect("CString::new should succeed after null removal");
    *out_json = c_string.into_raw();
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn current_alpn_uses_single_current_protocol() {
        let alpns = build_alpn_protocols(CURRENT_WIRE_FORMAT).unwrap();

        assert_eq!(alpns, vec![ALPN_CURRENT.to_vec()]);
    }

    #[test]
    fn current_alpn_rejects_unknown_wire_format() {
        let error = build_alpn_protocols(CURRENT_WIRE_FORMAT + 1).unwrap_err();

        assert!(error.contains("requested_wire_format"));
    }

    #[test]
    fn client_hello_uses_current_wire_format_and_current_bitmap() {
        let packet = build_client_hello_packet(CURRENT_WIRE_FORMAT, 41, b"engine-sr")
            .unwrap();

        assert_eq!(packet[5], CURRENT_WIRE_FORMAT);
        assert_eq!(
            u32::from_le_bytes(
                packet[HEADER_LENGTH + 4..HEADER_LENGTH + 8]
                    .try_into()
                    .unwrap()
            ),
            0x0001
        );
        assert_eq!(
            u32::from_le_bytes(
                packet[HEADER_LENGTH + 8..HEADER_LENGTH + 12]
                    .try_into()
                    .unwrap()
            ),
            0x0001
        );
    }

    #[test]
    fn client_hello_rejects_legacy_fallback() {
        let error = build_client_hello_packet(CURRENT_WIRE_FORMAT, true, 41, b"engine-sr")
            .unwrap_err();

        assert!(error.contains("legacy wire fallback"));
    }
}
