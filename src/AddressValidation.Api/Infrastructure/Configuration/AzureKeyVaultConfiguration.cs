using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AddressValidation.Api.Infrastructure.Configuration;

/// <summary>
/// Extension methods for Azure Key Vault configuration
/// </summary>
public static class AzureKeyVaultConfiguration
{
    /// <summary>
    /// Add Azure Key Vault to the configuration
    /// </summary>
    public static IConfigurationBuilder AddAzureKeyVault(
        this IConfigurationBuilder configurationBuilder,
        WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("AzureKeyVault:Enabled"))
        {
            return configurationBuilder;
        }

        var vaultUri = builder.Configuration.GetValue<string>("AzureKeyVault:VaultUri");
        if (string.IsNullOrEmpty(vaultUri))
        {
            throw new InvalidOperationException(
                "AzureKeyVault:VaultUri must be configured when AzureKeyVault:Enabled is true");
        }

        var credential = new DefaultAzureCredential();

        configurationBuilder.AddAzureKeyVault(
            new Uri(vaultUri),
            credential,
            new Azure.Extensions.AspNetCore.Configuration.Secrets.KeyVaultSecretManager());

        return configurationBuilder;
    }
}
