using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SplitIt.Tests;

/// <summary>
/// Phase 12 — HTTPS: Let's Encrypt automated renewal, 80→443 redirect, reload mechanism.
/// Validates that the infrastructure files required for Phase 12 are present and correct.
/// </summary>
public class Phase12HttpsTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string ReadRepoFile(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"File not found: {relativePath}");
        return File.ReadAllText(fullPath);
    }

    // --- certbot-renew.sh ---

    [Fact]
    public void CertbotRenewScript_Exists()
    {
        var script = ReadRepoFile("docker/proxy/certbot-renew.sh");
        Assert.False(string.IsNullOrWhiteSpace(script));
    }

    [Fact]
    public void CertbotRenewScript_ContainsRenewalLoop()
    {
        var script = ReadRepoFile("docker/proxy/certbot-renew.sh");
        Assert.Contains("certbot renew", script);
        Assert.Contains("while true", script);
        Assert.Contains("sleep", script);
    }

    [Fact]
    public void CertbotRenewScript_ContainsDeployHookSentinel()
    {
        var script = ReadRepoFile("docker/proxy/certbot-renew.sh");
        Assert.Contains("deploy-hook", script);
        Assert.Contains(".reload-trigger", script);
    }

    [Fact]
    public void CertbotRenewScript_ContainsInitialIssuance()
    {
        var script = ReadRepoFile("docker/proxy/certbot-renew.sh");
        Assert.Contains("certonly", script);
        Assert.Contains("--webroot", script);
        Assert.Contains("--non-interactive", script);
    }

    // --- entrypoint.sh reload watcher ---

    [Fact]
    public void Entrypoint_ContainsReloadWatcher()
    {
        var entrypoint = ReadRepoFile("docker/proxy/entrypoint.sh");
        Assert.Contains(".reload-trigger", entrypoint);
        Assert.Contains("nginx -s reload", entrypoint);
    }

    [Fact]
    public void Entrypoint_WatcherRunsInBackground()
    {
        var entrypoint = ReadRepoFile("docker/proxy/entrypoint.sh");
        Assert.Contains(") &", entrypoint);
        Assert.Contains("sleep 60", entrypoint);
    }

    // --- docker-compose.yml ---
    // NOTE: certbot-renewer service tests removed — service was removed in OPTION A architecture.
    // TLS termination and certificate renewal are now handled by the VPS reverse-proxy.

    // --- nginx.conf.template: 80→443 redirect & ACME ---

    [Fact]
    public void NginxTemplate_HasHttpToHttpsRedirect()
    {
        var template = ReadRepoFile("docker/proxy/nginx.conf.template");
        Assert.Contains("return 301 https://", template);
    }

    [Fact]
    public void NginxTemplate_HasAcmeChallengePath()
    {
        var template = ReadRepoFile("docker/proxy/nginx.conf.template");
        Assert.Contains(".well-known/acme-challenge", template);
    }

    [Fact]
    public void NginxTemplate_AcmePathHasNoRedirect()
    {
        var template = ReadRepoFile("docker/proxy/nginx.conf.template");
        // Multi-site gateway: there are multiple HTTP server blocks (default catch-all + per-domain).
        // Find the HTTP block that serves ${DOMAIN} with ACME challenge and redirect, not the default catch-all (return 444).
        var httpServerMatches = Regex.Matches(template, @"listen\s+\$\{HTTP_LISTEN_PORT\}.*?(?=\n\s*server\s*\{|\Z)",
            RegexOptions.Singleline);
        Assert.True(httpServerMatches.Count > 0, "HTTP server block not found");
        var httpBlock = httpServerMatches.Cast<Match>()
            .Select(m => m.Value)
            .FirstOrDefault(b => b.Contains("${DOMAIN}") || (b.Contains("acme-challenge") && b.Contains("return 301")));
        // Fallback to any block with acme-challenge if DOMAIN filter fails (backwards compat with single-site template)
        httpBlock ??= httpServerMatches.Cast<Match>()
            .Select(m => m.Value)
            .FirstOrDefault(b => b.Contains("acme-challenge"));
        Assert.True(httpBlock != null, "HTTP server block with ACME challenge not found");
        Assert.Contains("acme-challenge", httpBlock);
        Assert.Contains("return 301", httpBlock);

        var acmeLocation = Regex.Match(httpBlock, @"location\s+/\.well-known/acme-challenge/.*?\}",
            RegexOptions.Singleline);
        Assert.True(acmeLocation.Success, "ACME location block not found");
        var acmeBlock = acmeLocation.Value;
        Assert.DoesNotContain("return 301", acmeBlock);
    }

    // --- docs/HTTPS.md ---

    [Fact]
    public void HttpsDocs_DocumentAutomatedRenewal()
    {
        var docs = ReadRepoFile("docs/HTTPS.md");
        Assert.Contains("certbot-renewer", docs);
        Assert.Contains("sentinel", docs);
        Assert.Contains("Phase 12", docs);
    }

    [Fact]
    public void HttpsDocs_DocumentManualRenewal()
    {
        var docs = ReadRepoFile("docs/HTTPS.md");
        Assert.Contains("certbot renew", docs);
        Assert.Contains("nginx -s reload", docs);
    }

    [Fact]
    public void HttpsDocs_DocumentReloadMechanism()
    {
        var docs = ReadRepoFile("docs/HTTPS.md");
        Assert.Contains("Automated reload", docs);
        Assert.Contains("deploy-hook", docs);
    }

    // --- ssl-params.conf: TLS hardening ---

    [Fact]
    public void SslParams_DisableObsoleteProtocols()
    {
        var ssl = ReadRepoFile("docker/proxy/snippets/ssl-params.conf");
        Assert.Contains("TLSv1.2", ssl);
        Assert.Contains("TLSv1.3", ssl);
        Assert.DoesNotContain("TLSv1.0", ssl);
        Assert.DoesNotContain("TLSv1.1", ssl);
    }

    [Fact]
    public void SslParams_HasHstsInSecurityHeaders()
    {
        var headers = ReadRepoFile("docker/proxy/snippets/security-headers.conf");
        Assert.Contains("Strict-Transport-Security", headers);
        Assert.Contains("max-age=31536000", headers);
        Assert.Contains("preload", headers);
    }

    // --- .dockerignore certificate exclusions ---

    [Fact]
    public void DockerIgnore_BlocksCertificateFiles()
    {
        var dockerignore = ReadRepoFile(".dockerignore");
        Assert.Contains("*.pem", dockerignore);
        Assert.Contains("*.key", dockerignore);
        Assert.Contains("*.crt", dockerignore);
    }

}
