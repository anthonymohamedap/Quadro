using System;
using System.IO;
using QuadroApp.Service.Security;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-33 — secrets management.</summary>
public class SecretStoreTests : IDisposable
{
    private readonly string _dataDir;

    public SecretStoreTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "QuadroSecretTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);
        Environment.SetEnvironmentVariable(SecretStore.PasswordEnvVar, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(SecretStore.PasswordEnvVar, null);
        try { Directory.Delete(_dataDir, recursive: true); } catch { }
    }

    [Fact]
    public void InjectPassword_SqliteString_PassesThroughUntouched()
    {
        const string cs = "Data Source=quadro.db";
        Assert.Equal(cs, SecretStore.InjectPassword(cs, _dataDir));
    }

    [Fact]
    public void InjectPassword_NoSecretAvailable_ReturnsInputUnchanged()
    {
        const string cs = "Host=localhost;Database=quadrodb;Username=quadro;Password=__SECRET__";
        Assert.Equal(cs, SecretStore.InjectPassword(cs, _dataDir));
        Assert.True(SecretStore.HasUnresolvedPlaceholder(cs));
    }

    [Fact]
    public void InjectPassword_EnvVar_ReplacesPlaceholder()
    {
        Environment.SetEnvironmentVariable(SecretStore.PasswordEnvVar, "envpw123");
        var cs = SecretStore.InjectPassword("Host=x;Password=__SECRET__", _dataDir);
        Assert.Equal("Host=x;Password=envpw123", cs);
        Assert.False(SecretStore.HasUnresolvedPlaceholder(cs));
    }

    [Fact]
    public void InjectPassword_LegacyChangeMe_AlsoReplaced()
    {
        Environment.SetEnvironmentVariable(SecretStore.PasswordEnvVar, "envpw123");
        var cs = SecretStore.InjectPassword("Host=x;Password=CHANGE_ME", _dataDir);
        Assert.Equal("Host=x;Password=envpw123", cs);
    }

    [Fact]
    public void InjectPassword_RealPasswordPresent_LeftAlone()
    {
        Environment.SetEnvironmentVariable(SecretStore.PasswordEnvVar, "should-not-be-used");
        const string cs = "Host=x;Password=alreadyset";
        Assert.Equal(cs, SecretStore.InjectPassword(cs, _dataDir));
    }

    [Fact]
    public void InjectPassword_NoPasswordKey_AppendsFromSecret()
    {
        Environment.SetEnvironmentVariable(SecretStore.PasswordEnvVar, "pw");
        var cs = SecretStore.InjectPassword("Host=x;Username=quadro", _dataDir);
        Assert.Equal("Host=x;Username=quadro;Password=pw", cs);
    }

    [Fact]
    public void StoreAndResolve_RoundTrips()
    {
        SecretStore.StorePassword("geheim!123", _dataDir);
        Assert.Equal("geheim!123", SecretStore.ResolvePassword(_dataDir));
    }

    [Fact]
    public void ResolvePassword_EnvVarWinsOverFile()
    {
        SecretStore.StorePassword("file-pw", _dataDir);
        Environment.SetEnvironmentVariable(SecretStore.PasswordEnvVar, "env-pw");
        Assert.Equal("env-pw", SecretStore.ResolvePassword(_dataDir));
    }

    [Fact]
    public void ResolvePassword_NothingConfigured_ReturnsNull()
    {
        Assert.Null(SecretStore.ResolvePassword(_dataDir));
    }

    [Fact]
    public void Redact_HidesPassword()
    {
        var redacted = SecretStore.Redact("Host=x;Password=supergeheim;Port=5432");
        Assert.DoesNotContain("supergeheim", redacted);
        Assert.Contains("Password=***", redacted);
    }
}
