# com.nnrp.client

Version: 1.0.0-preview.4

This tracked package definition lets OpenUPM discover package metadata directly from the repository.

Installable UPM tarballs are produced by CI and published as GitHub Release assets for each tagged version.
The generated package contains the client, core, NativeBridge, and all four provider assemblies plus
transport-scoped native plugins. It never contains the server role or NuGet runtime layout.

The Preview4 release remains gated on importing the generated archive into a Unity 2022.3 validation
project. Use the validated GitHub Release archive rather than copying generated files from the source tree.

Full protocol and SDK documentation: https://nagareworks.github.io/nnrp-doc/
