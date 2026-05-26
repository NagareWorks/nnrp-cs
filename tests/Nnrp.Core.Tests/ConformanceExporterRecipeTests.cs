using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nnrp.ConformanceExporter;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class ConformanceExporterRecipeTests
    {
        [Fact]
        public void BuildManifestJsonFromLocalPreview3RecipeMatchesExpectedShape()
        {
            var recipeManifestPath = Path.Combine(
                RepoRoot(),
                "tests",
                "Nnrp.Core.Tests",
                "Fixtures",
                "preview3-semantic-vectors.json");

            var manifestJson = Program.BuildManifestJson("nnrp-1-preview3", recipeManifestPath);

            using var document = JsonDocument.Parse(manifestJson);
            var root = document.RootElement;
            Assert.Equal("nnrp-1-preview3", root.GetProperty("protocol_version").GetString());
            Assert.Equal("nnrp-cs", root.GetProperty("generator").GetString());

            var vectors = root.GetProperty("vectors").EnumerateArray().ToArray();
            Assert.Equal(12, vectors.Length);
            Assert.Equal("current.header.frame_submit_ack_required_keyframe", vectors[0].GetProperty("name").GetString());
            Assert.Equal("current.typed_payload.frame_region", vectors[^1].GetProperty("name").GetString());
            Assert.Equal("746f6b6175766964656f657674", vectors[^1].GetProperty("hex").GetString());
        }

        [Fact]
        public void BuildManifestJsonFromSharedPreview3RecipeMatchesExpectedShape()
        {
            var recipeManifestPath = ResolveSharedRecipeManifestPath();
            if (recipeManifestPath == null)
            {
                return;
            }

            var manifestJson = Program.BuildManifestJson("nnrp-1-preview3", recipeManifestPath);

            using var document = JsonDocument.Parse(manifestJson);
            var root = document.RootElement;
            Assert.Equal("nnrp-1-preview3", root.GetProperty("protocol_version").GetString());
            Assert.Equal("nnrp-cs", root.GetProperty("generator").GetString());

            var vectors = root.GetProperty("vectors").EnumerateArray().ToArray();
            Assert.Equal(12, vectors.Length);
            Assert.Equal("current.header.frame_submit_ack_required_keyframe", vectors[0].GetProperty("name").GetString());
            Assert.Equal("current.typed_payload.frame_region", vectors[^1].GetProperty("name").GetString());
            Assert.Equal("746f6b6175766964656f657674", vectors[^1].GetProperty("hex").GetString());
        }

        [Fact]
        public void BuildManifestJsonRejectsMissingRecipeManifestPath()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");
            var error = Assert.Throws<ArgumentException>(() => Program.BuildManifestJson("nnrp-1-preview3", path));

            Assert.Contains("Recipe manifest path does not exist", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildManifestJsonRejectsProtocolMismatch()
        {
            var path = WriteTempRecipeManifest("{\"protocol_version\":\"nnrp-0-invalid\",\"vectors\":[]}");

            try
            {
                var error = Assert.Throws<ArgumentException>(() => Program.BuildManifestJson("nnrp-1-preview3", path));
                Assert.Contains("protocol version does not match requested export", error.Message, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void BuildManifestJsonRejectsUnsupportedRecipeType()
        {
            var path = WriteTempRecipeManifest("{\"protocol_version\":\"nnrp-1-preview3\",\"vectors\":[{\"recipe_type\":\"unknown\",\"name\":\"bad\"}]}");

            try
            {
                var error = Assert.Throws<ArgumentException>(() => Program.BuildManifestJson("nnrp-1-preview3", path));
                Assert.Contains("Unsupported semantic vector recipe type", error.Message, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string? ResolveSharedRecipeManifestPath()
        {
            var configuredPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_RECIPE_MANIFEST");
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            {
                return configuredPath;
            }

            var repoRoot = RepoRoot();
            var candidates = new[]
            {
                Path.Combine(repoRoot, "nnrp-conformance-action", "protocol", "nnrp-1-preview3", "vectors", "semantic-vectors.json"),
                Path.GetFullPath(Path.Combine(repoRoot, "..", "nnrp-conformance", "protocol", "nnrp-1-preview3", "vectors", "semantic-vectors.json")),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string RepoRoot()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        }

        private static string WriteTempRecipeManifest(string payload)
        {
            var path = Path.Combine(Path.GetTempPath(), $"nnrp-conformance-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, payload);
            return path;
        }
    }
}
