namespace Theragraf.Functions.Services;

using Microsoft.Azure.Cosmos;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

/// <summary>
/// Azure Cosmos DB for NoSQL implementation of <see cref="IClientRepository"/>.
/// Database: theragraf   Container: clients   PartitionKey: /clientId
///
/// DOB handling:
///   The raw date-of-birth string is encrypted via <see cref="IRedactionMapEncryption"/>
///   before being written to Cosmos, and decrypted on read.  The API surface never
///   exposes the DOB; only the computed <c>AgeYears</c> is returned.
/// </summary>
public class CosmosClientRepository(
    CosmosClient            cosmosClient,
    string                  databaseName,
    string                  containerName,
    IRedactionMapEncryption encryption) : IClientRepository
{
    private readonly Container _container = cosmosClient.GetContainer(databaseName, containerName);

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<ClientDemographicsResponse?> GetAsync(
        string clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<ClientDocument>(
                clientId, new PartitionKey(clientId), cancellationToken: cancellationToken);
            return MapToResponse(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    public async Task<ClientDemographicsResponse> UpsertAsync(
        string clientId, UpsertClientDemographicsRequest request,
        CancellationToken cancellationToken = default)
    {
        // Read existing document (if any) so we preserve encrypted DOB when the caller
        // does not supply a new one.
        ClientDocument? existing = null;
        try
        {
            var existing_ = await _container.ReadItemAsync<ClientDocument>(
                clientId, new PartitionKey(clientId), cancellationToken: cancellationToken);
            existing = existing_.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }

        var doc = new ClientDocument
        {
            Id       = clientId,
            ClientId = clientId,
            Sex      = request.Sex.ToString(),
            PriorDiagnoses       = request.PriorDiagnoses,
            FunctionalLimitations = request.FunctionalLimitations,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // DOB handling:
        //  - null in request  → preserve existing encrypted value (no change)
        //  - empty string     → clear the DOB (set null)
        //  - non-empty string → encrypt and store
        if (request.DateOfBirth is null)
        {
            doc.EncryptedDateOfBirth = existing?.EncryptedDateOfBirth;
        }
        else if (string.IsNullOrWhiteSpace(request.DateOfBirth))
        {
            doc.EncryptedDateOfBirth = null;
        }
        else
        {
            doc.EncryptedDateOfBirth = encryption.Encrypt(request.DateOfBirth);
        }

        await _container.UpsertItemAsync(doc, new PartitionKey(clientId), cancellationToken: cancellationToken);
        return MapToResponse(doc);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private ClientDemographicsResponse MapToResponse(ClientDocument doc)
    {
        var sex = Enum.TryParse<BiologicalSex>(doc.Sex, out var parsed)
            ? parsed
            : BiologicalSex.NotSpecified;

        int? ageYears = null;
        if (!string.IsNullOrWhiteSpace(doc.EncryptedDateOfBirth))
        {
            try
            {
                var dobStr = encryption.Decrypt(doc.EncryptedDateOfBirth);
                if (DateOnly.TryParse(dobStr, out var dob))
                {
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    ageYears = today.Year - dob.Year;
                    if (today < dob.AddYears(ageYears.Value)) ageYears--;
                }
            }
            catch
            {
                // Decryption failure (e.g. wrong key in local dev) — omit age silently.
                ageYears = null;
            }
        }

        return new ClientDemographicsResponse(
            ClientId:             doc.ClientId,
            AgeYears:             ageYears,
            Sex:                  sex,
            PriorDiagnoses:       doc.PriorDiagnoses,
            FunctionalLimitations: doc.FunctionalLimitations,
            UpdatedAt:            doc.UpdatedAt);
    }
}
