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

    [Fact]
    public void Compose_HasCertbotRenewerService()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        Assert.Contains("certbot-renewer", compose);
    }

    [Fact]
    public void Compose_CertbotRenewerHasLetsencryptProfile()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        Assert.Contains("letsencrypt", compose);
    }

    [Fact]
    public void Compose_CertbotRenewerHasCorrectVolumes()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        Assert.Contains("certbot_certs", compose);
        Assert.Contains("certbot_www", compose);
        Assert.Contains("certbot-renew.sh", compose);
    }

    [Fact]
    public void Compose_CertbotRenewerHasResourceLimits()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        var renewerSection = ExtractServiceSection(compose, "certbot-renewer");
        Assert.Contains("cpus", renewerSection);
        Assert.Contains("memory", renewerSection);
    }

    [Fact]
    public void Compose_CertbotRenewerDependsOnProxy()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        var renewerSection = ExtractServiceSection(compose, "certbot-renewer");
        Assert.Contains("proxy", renewerSection);
        Assert.Contains("service_healthy", renewerSection);
    }

    // --- .env.example ---

    [Fact]
    public void EnvExample_DocumentsComposeProfiles()
    {
        var env = ReadRepoFile(".env.example");
        Assert.Contains("COMPOSE_PROFILES", env);
        Assert.Contains("letsencrypt", env);
    }

    [Fact]
    public void EnvExample_DocumentsRenewalInterval()
    {
        var env = ReadRepoFile(".env.example");
        Assert.Contains("RENEWAL_INTERVAL", env);
    }

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
        var httpServerMatch = Regex.Match(template, @"listen\s+\$\{HTTP_LISTEN_PORT\}.*?(?=\n\s*server\s*\{|\Z)",
            RegexOptions.Singleline);
        Assert.True(httpServerMatch.Success, "HTTP server block not found");
        var httpBlock = httpServerMatch.Value;
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

    /// <summary>
    /// Extracts the YAML block for a specific service from docker-compose.yml.
    /// Returns everything from the service name until the next top-level service or section.
    /// </summary>
    private static string ExtractServiceSection(string compose, string serviceName)
    {
        var pattern = $@"  {serviceName}:\s*\n(.*?)(?=\n  [a-z]|\n  #|\Z)";
        var match = Regex.Match(compose, pattern, RegexOptions.Singleline);
        Assert.True(match.Success, $"Service '{serviceName}' not found in docker-compose.yml");
        return match.Value;
    }
}
