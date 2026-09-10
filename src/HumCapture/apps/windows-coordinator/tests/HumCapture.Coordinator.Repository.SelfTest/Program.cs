using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HumCapture.Coordinator.Repository;
using Microsoft.Data.Sqlite;

var tests = new (string Name, Action Body)[]
{
    ("HC-REP-RUNTIME-001 initialize creates exact repository surface", InitializeCreatesSurface),
    ("HC-REP-RUNTIME-002 supported repository reopens for mutation", SupportedRepositoryOpens),
    ("HC-REP-RUNTIME-003 descriptor and catalog metadata agree", MetadataAgrees),
    ("HC-REP-RUNTIME-004 initialization never overwrites", ReinitializeRefuses),
    ("HC-REP-RUNTIME-005 nonempty root is refused", NonemptyRootRefused),
    ("HC-REP-RUNTIME-006 relative root is refused", RelativeRootRefused),
    ("HC-REP-RUNTIME-007 unsupported major is read-only without writes", UnsupportedMajorIsReadOnly),
    ("HC-REP-RUNTIME-008 older version requires explicit migration", OlderVersionIsReadOnly),
    ("HC-REP-RUNTIME-009 unknown required feature is read-only", UnknownFeatureIsReadOnly),
    ("HC-REP-RUNTIME-010 unknown field on supported descriptor is invalid", UnknownFieldIsInvalid),
    ("HC-REP-RUNTIME-011 missing catalog is refused", MissingCatalogRefused),
    ("HC-REP-RUNTIME-012 corrupt catalog is refused", CorruptCatalogRefused),
    ("HC-REP-RUNTIME-013 metadata mismatch is refused", MetadataMismatchRefused),
    ("HC-REP-RUNTIME-014 descriptor publication leaves no temporary file", NoTemporaryDescriptorRemains),
    ("HC-REP-RUNTIME-015 empty repository UUID is refused", EmptyRepositoryIdRefused),
    ("HC-REP-RUNTIME-016 hard-linked catalog is refused", HardLinkedCatalogRefused),
    ("HC-REP-RUNTIME-017 retained initialization lock is not removed", RetainedInitializationLockRefused),
    ("HC-REP-RUNTIME-018 verified stage creates atomic journal and index", VerifiedStageCreatesJournal),
    ("HC-REP-RUNTIME-019 exact stage admission replay is idempotent", StageReplayIsIdempotent),
    ("HC-REP-RUNTIME-020 missing staged package is refused", MissingStagedPackageRefused),
    ("HC-REP-RUNTIME-021 existing final destination is refused", ExistingDestinationRefused),
    ("HC-REP-RUNTIME-022 changed staged bytes are refused", ChangedStagedBytesRefused),
    ("HC-REP-RUNTIME-023 false verified aggregate is refused", FalseVerifiedAggregateRefused),
    ("HC-REP-RUNTIME-024 artifact verification mismatch is refused", ArtifactVerificationMismatchRefused),
    ("HC-REP-RUNTIME-025 verification record hash mismatch is refused", VerificationHashMismatchRefused),
    ("HC-REP-RUNTIME-026 unknown verification field is refused", UnknownVerificationFieldRefused),
    ("HC-REP-RUNTIME-027 journal identity conflict is refused", JournalIdentityConflictRefused),
    ("HC-REP-RUNTIME-028 immutable verification collision is refused", ImmutableVerificationCollisionRefused),
    ("HC-REP-RUNTIME-029 read-only repository refuses journal mutation", ReadOnlyJournalMutationRefused),
    ("HC-REP-RUNTIME-030 hard-linked staged artifact is refused", HardLinkedStagedArtifactRefused),
    ("HC-REP-RUNTIME-031 failed index insert rolls back journal rows", IndexFailureRollsBackJournal)
};

var failures = 0;
foreach (var (name, body) in tests)
{
    try
    {
        body();
        await Console.Out.WriteLineAsync($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        await Console.Error.WriteLineAsync($"FAIL {name}: {exception}");
    }
}

await Console.Out.WriteLineAsync($"SUMMARY total={tests.Length} passed={tests.Length - failures} failed={failures}");
return failures == 0 ? 0 : 1;

static void InitializeCreatesSurface()
{
    WithRepository((root, _) =>
    {
        True(new[] { "catalog", "quarantine", "repository.json", "staging", "subjects" }.SequenceEqual(
            Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order()));
        True(File.Exists(Path.Combine(root, "catalog", "humcapture.sqlite3")));
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "repository.json")));
        Equal(9, json.RootElement.EnumerateObject().Count());
        Equal("1.3.0", json.RootElement.GetProperty("interface_version").GetString());
    });
}

static void SupportedRepositoryOpens()
{
    WithRepository((root, service) =>
    {
        var opened = service.Open(root);
        Equal(RepositoryAccessMode.MutationAllowed, opened.AccessMode);
        Equal("SUPPORTED", opened.ReasonCode);
    });
}

static void MetadataAgrees()
{
    WithRepository((root, _) =>
    {
        using var descriptor = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "repository.json")));
        using var connection = OpenCatalog(root, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT repository_id, created_by_windows_account FROM repository_metadata WHERE singleton_id = 1";
        using var reader = command.ExecuteReader();
        True(reader.Read());
        Equal(descriptor.RootElement.GetProperty("repository_id").GetString(), reader.GetString(0));
        Equal("TEST\\operator", reader.GetString(1));
    });
}

static void ReinitializeRefuses()
{
    WithRepository((root, service) =>
    {
        var before = Hash(Path.Combine(root, "repository.json"));
        Throws(RepositoryErrorCode.AlreadyInitialized, () => service.Initialize(root, "TEST\\operator"));
        Equal(before, Hash(Path.Combine(root, "repository.json")));
    });
}

static void NonemptyRootRefused()
{
    WithTemporaryRoot(root =>
    {
        File.WriteAllText(Path.Combine(root, "unrelated.txt"), "retain");
        Throws(RepositoryErrorCode.RootNotEmpty, () => new RepositoryService().Initialize(root, "TEST\\operator"));
        Equal("retain", File.ReadAllText(Path.Combine(root, "unrelated.txt")));
    });
}

static void RelativeRootRefused() =>
    Throws(RepositoryErrorCode.RootPathNotAbsolute, () => new RepositoryService().Initialize("relative-repository", "TEST\\operator"));

static void UnsupportedMajorIsReadOnly()
{
    WithRepository((root, service) =>
    {
        EditDescriptor(root, descriptor => descriptor["interface_version"] = "2.0.0");
        AssertReadOnlyWithoutCatalogWrite(root, service, "UNSUPPORTED_INTERFACE_MAJOR");
    });
}

static void OlderVersionIsReadOnly()
{
    WithRepository((root, service) =>
    {
        EditDescriptor(root, descriptor => descriptor["interface_version"] = "1.2.0");
        AssertReadOnlyWithoutCatalogWrite(root, service, "MIGRATION_REQUIRED");
    });
}

static void UnknownFeatureIsReadOnly()
{
    WithRepository((root, service) =>
    {
        EditDescriptor(root, descriptor =>
        {
            var features = (List<object?>)descriptor["required_features"]!;
            features.Add("FUTURE_REQUIRED_FEATURE");
        });
        AssertReadOnlyWithoutCatalogWrite(root, service, "UNKNOWN_REQUIRED_FEATURE");
    });
}

static void UnknownFieldIsInvalid()
{
    WithRepository((root, service) =>
    {
        EditDescriptor(root, descriptor => descriptor["unexpected"] = true);
        Throws(RepositoryErrorCode.DescriptorInvalid, () => service.Open(root));
    });
}

static void MissingCatalogRefused()
{
    WithRepository((root, service) =>
    {
        File.Delete(Path.Combine(root, "catalog", "humcapture.sqlite3"));
        Throws(RepositoryErrorCode.CatalogMissing, () => service.Open(root));
    });
}

static void CorruptCatalogRefused()
{
    WithRepository((root, service) =>
    {
        File.WriteAllText(Path.Combine(root, "catalog", "humcapture.sqlite3"), "not sqlite");
        Throws(RepositoryErrorCode.CatalogInvalid, () => service.Open(root));
    });
}

static void MetadataMismatchRefused()
{
    WithRepository((root, service) =>
    {
        using (var connection = OpenCatalog(root, SqliteOpenMode.ReadWrite))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE repository_metadata SET created_by_windows_account = 'TEST\\other' WHERE singleton_id = 1";
            command.ExecuteNonQuery();
        }
        Throws(RepositoryErrorCode.CatalogMetadataMismatch, () => service.Open(root));
    });
}

static void NoTemporaryDescriptorRemains()
{
    WithRepository((root, _) =>
    {
        True(!Directory.EnumerateFiles(root, ".repository-*.tmp").Any());
        True(!File.Exists(Path.Combine(root, ".humcapture-initialize.lock")));
    });
}

static void EmptyRepositoryIdRefused()
{
    WithTemporaryRoot(root =>
    {
        try
        {
            _ = new RepositoryService().Initialize(root, "TEST\\operator", repositoryId: Guid.Empty);
            throw new InvalidOperationException("Expected ArgumentException.");
        }
        catch (ArgumentException exception)
        {
            True(exception.ParamName == "repositoryId");
        }
    });
}

static void HardLinkedCatalogRefused()
{
    WithRepository((root, service) =>
    {
        var catalog = Path.Combine(root, "catalog", "humcapture.sqlite3");
        CreateHardLink(Path.Combine(root, "catalog", "catalog-hardlink.sqlite3"), catalog);
        Throws(RepositoryErrorCode.UnsafePath, () => service.Open(root));
    });
}

static void RetainedInitializationLockRefused()
{
    WithTemporaryRoot(root =>
    {
        var lockPath = Path.Combine(root, ".humcapture-initialize.lock");
        File.WriteAllText(lockPath, "retained-for-inspection");
        Throws(RepositoryErrorCode.InitializationInProgress, () => new RepositoryService().Initialize(root, "TEST\\operator"));
        Equal("retained-for-inspection", File.ReadAllText(lockPath));
    });
}

static void VerifiedStageCreatesJournal()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        var result = service.RegisterStagedVerifiedPackage(root, fixture.Registration);
        Equal("STAGED_VERIFIED", result.State);
        Equal(1L, result.Revision);
        Equal(false, result.WasAlreadyPresent);
        Equal(fixture.StagingRelativePath, result.StagingRelativePath);
        Equal(fixture.DestinationRelativePath, result.DestinationRelativePath);
        Equal(fixture.VerificationRelativePath, result.VerificationRecordRelativePath);
        True(File.ReadAllBytes(Path.Combine(root, fixture.VerificationRelativePath.Replace('/', Path.DirectorySeparatorChar)))
            .SequenceEqual(fixture.Registration.VerificationRecordUtf8.ToArray()));

        using var connection = OpenCatalog(root, SqliteOpenMode.ReadOnly);
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_transactions WHERE state = 'STAGED_VERIFIED' AND revision = 1"));
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_transitions WHERE transition_sequence = 1 AND from_state IS NULL AND to_state = 'STAGED_VERIFIED'"));
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_record_index WHERE record_kind = 'verifications'"));
    });
}

static void StageReplayIsIdempotent()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        _ = service.RegisterStagedVerifiedPackage(root, fixture.Registration);
        var replay = service.RegisterStagedVerifiedPackage(root, fixture.Registration);
        True(replay.WasAlreadyPresent);
        using var connection = OpenCatalog(root, SqliteOpenMode.ReadOnly);
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_transactions"));
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_transitions"));
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_record_index"));
    });
}

static void MissingStagedPackageRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        Directory.Delete(Path.Combine(root, fixture.StagingRelativePath.Replace('/', Path.DirectorySeparatorChar)), recursive: true);
        Throws(RepositoryErrorCode.StagedPackageMissing, () => service.RegisterStagedVerifiedPackage(root, fixture.Registration));
        AssertNoJournalRows(root);
    });
}

static void ExistingDestinationRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        Directory.CreateDirectory(Path.Combine(root, fixture.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        Throws(RepositoryErrorCode.JournalConflict, () => service.RegisterStagedVerifiedPackage(root, fixture.Registration));
        AssertNoJournalRows(root);
    });
}

static void ChangedStagedBytesRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        File.AppendAllText(Path.Combine(root, fixture.StagingRelativePath.Replace('/', Path.DirectorySeparatorChar), "events.json"), "changed");
        Throws(RepositoryErrorCode.EvidenceMismatch, () => service.RegisterStagedVerifiedPackage(root, fixture.Registration));
        AssertNoJournalRows(root);
    });
}

static void FalseVerifiedAggregateRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root, verificationEdit: record =>
        {
            var checks = (List<object?>)record["checks"]!;
            var first = (Dictionary<string, object?>)checks[0]!;
            first["disposition"] = "NOT_ASSESSED";
        });
        Throws(RepositoryErrorCode.EvidenceMismatch, () => service.RegisterStagedVerifiedPackage(root, fixture.Registration));
        AssertNoJournalRows(root);
    });
}

static void ArtifactVerificationMismatchRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root, verificationEdit: record =>
        {
            var results = (List<object?>)record["artifact_results"]!;
            var first = (Dictionary<string, object?>)results[0]!;
            first["observed_sha256"] = new string('f', 64);
        });
        Throws(RepositoryErrorCode.EvidenceMismatch, () => service.RegisterStagedVerifiedPackage(root, fixture.Registration));
        AssertNoJournalRows(root);
    });
}

static void VerificationHashMismatchRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        var registration = fixture.Registration with { VerificationRecordContentSha256 = new string('0', 64) };
        Throws(RepositoryErrorCode.EvidenceMismatch, () => service.RegisterStagedVerifiedPackage(root, registration));
        AssertNoJournalRows(root);
    });
}

static void UnknownVerificationFieldRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root, verificationEdit: record => record["unexpected"] = true);
        Throws(RepositoryErrorCode.EvidenceMismatch, () => service.RegisterStagedVerifiedPackage(root, fixture.Registration));
        AssertNoJournalRows(root);
    });
}

static void JournalIdentityConflictRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        _ = service.RegisterStagedVerifiedPackage(root, fixture.Registration);
        var conflict = fixture.Registration with { TransactionId = Guid.Parse("90000000-0000-4000-8000-000000000099") };
        Throws(RepositoryErrorCode.JournalConflict, () => service.RegisterStagedVerifiedPackage(root, conflict));
        using var connection = OpenCatalog(root, SqliteOpenMode.ReadOnly);
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_transactions"));
    });
}

static void ImmutableVerificationCollisionRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        _ = service.RegisterStagedVerifiedPackage(root, fixture.Registration);
        var changedBytes = fixture.Registration.VerificationRecordUtf8.ToArray().Concat(" \n"u8.ToArray()).ToArray();
        var changed = fixture.Registration with
        {
            TransactionId = Guid.Parse("90000000-0000-4000-8000-000000000098"),
            VerificationRecordUtf8 = changedBytes,
            VerificationRecordContentSha256 = Convert.ToHexStringLower(SHA256.HashData(changedBytes))
        };
        Throws(RepositoryErrorCode.ImmutableRecordConflict, () => service.RegisterStagedVerifiedPackage(root, changed));
    });
}

static void ReadOnlyJournalMutationRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        EditDescriptor(root, descriptor => descriptor["interface_version"] = "2.0.0");
        Throws(RepositoryErrorCode.MutationNotAllowed, () => service.RegisterStagedVerifiedPackage(root, fixture.Registration));
        AssertNoJournalRows(root);
    });
}

static void HardLinkedStagedArtifactRefused()
{
    WithRepository((root, service) =>
    {
        var fixture = CreateStagedFixture(root);
        var stage = Path.Combine(root, fixture.StagingRelativePath.Replace('/', Path.DirectorySeparatorChar));
        CreateHardLink(Path.Combine(stage, "events-hardlink.json"), Path.Combine(stage, "events.json"));
        Throws(RepositoryErrorCode.UnsafePath, () => service.RegisterStagedVerifiedPackage(root, fixture.Registration));
        AssertNoJournalRows(root);
    });
}

static void IndexFailureRollsBackJournal()
{
    WithRepository((root, service) =>
    {
        var first = CreateStagedFixture(root);
        _ = service.RegisterStagedVerifiedPackage(root, first.Registration);
        var preparedSecond = CreateStagedFixture(root, 2);
        var conflictingRegistration = preparedSecond.Registration with { RecordIndexEntryId = first.Registration.RecordIndexEntryId };
        Throws(RepositoryErrorCode.CatalogWriteFailed, () => service.RegisterStagedVerifiedPackage(root, conflictingRegistration));
        using var connection = OpenCatalog(root, SqliteOpenMode.ReadOnly);
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_transactions"));
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_transitions"));
        Equal(1L, ScalarLong(connection, "SELECT count(*) FROM repository_record_index"));
    });
}

static (StagedVerifiedPackageRegistration Registration, string StagingRelativePath, string DestinationRelativePath, string VerificationRelativePath) CreateStagedFixture(
    string root,
    int variant = 1,
    Action<Dictionary<string, object?>>? verificationEdit = null)
{
    var transactionId = TestId(2000 + variant);
    var transitionId = TestId(2100 + variant);
    var operationId = TestId(2200 + variant);
    var indexId = TestId(2300 + variant);
    var subjectId = TestId(3000 + variant);
    var sessionId = TestId(3100 + variant);
    var trialId = TestId(3200 + variant);
    var sourceId = TestId(3300 + variant);
    var captureAttemptId = TestId(3400 + variant);
    var collectionAttemptId = TestId(3500 + variant);
    var packageId = TestId(3600 + variant);
    var verificationId = TestId(3700 + variant);
    var eventArtifactId = TestId(3800 + variant);
    var finalizationArtifactId = TestId(3900 + variant);
    var eventBytes = Encoding.UTF8.GetBytes($"{{\"event\":\"capture-stopped-{variant}\"}}\n");
    var finalizationBytes = Encoding.UTF8.GetBytes($"{{\"outcome\":\"device-loss-{variant}\"}}\n");
    var eventHash = Convert.ToHexStringLower(SHA256.HashData(eventBytes));
    var finalizationHash = Convert.ToHexStringLower(SHA256.HashData(finalizationBytes));
    var packageByteLength = ((ulong)eventBytes.Length + (ulong)finalizationBytes.Length).ToString();

    var artifacts = new List<object?>
    {
        Artifact(eventArtifactId, "events.json", "CAPTURE_EVENTS", eventBytes.Length, eventHash),
        Artifact(finalizationArtifactId, "finalization.json", "FINALIZATION_RECORD", finalizationBytes.Length, finalizationHash)
    };
    var inventory = $"events.json\t{eventBytes.Length}\t{eventHash}\nfinalization.json\t{finalizationBytes.Length}\t{finalizationHash}\n";
    var artifactSetHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
    var manifest = new Dictionary<string, object?>
    {
        ["schema_version"] = "1.0.0",
        ["package_id"] = packageId.ToString("D"),
        ["package_content_sha256"] = new string('0', 64),
        ["artifact_set_sha256"] = artifactSetHash,
        ["subject_id"] = subjectId.ToString("D"),
        ["session_id"] = sessionId.ToString("D"),
        ["trial_id"] = trialId.ToString("D"),
        ["source_id"] = sourceId.ToString("D"),
        ["capture_attempt_id"] = captureAttemptId.ToString("D"),
        ["source_kind"] = "UVC",
        ["source_boot_id"] = TestId(4000 + variant).ToString("D"),
        ["protocol_snapshot_id"] = TestId(4100 + variant).ToString("D"),
        ["protocol_snapshot_content_sha256"] = new string('a', 64),
        ["configuration_id"] = TestId(4200 + variant).ToString("D"),
        ["configuration_content_sha256"] = new string('b', 64),
        ["finalization_outcome"] = "FINALIZED_INCOMPLETE",
        ["finalization_reason"] = "Synthetic device-loss survivor package.",
        ["finalized_utc"] = "2026-09-10T11:00:00.000Z",
        ["artifact_count"] = artifacts.Count,
        ["package_byte_length"] = packageByteLength,
        ["artifacts"] = artifacts
    };
    var packageContentHash = CanonicalHash(manifest, "package_content_sha256");
    var independentlyComputedHash = variant switch
    {
        1 => "66ca3ba4016c3b0e7825542fb221e82deb4bdd65ca40b482868b1fda2410cb15",
        2 => "12ca611c9d1a2725d2f870e9c58328e63924e9bbb17dc777c8353bf7a9b65dc7",
        _ => throw new InvalidOperationException("No independent canonical-hash oracle exists for this fixture variant.")
    };
    Equal(independentlyComputedHash, packageContentHash);
    manifest["package_content_sha256"] = packageContentHash;

    var stagingRelativePath = $"staging/{collectionAttemptId:D}/{packageId:D}";
    var destinationRelativePath = $"subjects/{subjectId:D}/sessions/{sessionId:D}/packages/{packageId:D}";
    var verificationRelativePath = $"subjects/{subjectId:D}/sessions/{sessionId:D}/records/verifications/{verificationId:D}.json";
    var stagingPath = Path.Combine(root, stagingRelativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(stagingPath);
    File.WriteAllBytes(Path.Combine(stagingPath, "events.json"), eventBytes);
    File.WriteAllBytes(Path.Combine(stagingPath, "finalization.json"), finalizationBytes);
    File.WriteAllBytes(Path.Combine(stagingPath, "package-manifest.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = true }));

    var checks = new List<object?>();
    foreach (var name in new[]
    {
        "MANIFEST_SCHEMA", "PACKAGE_IDENTITY", "PATH_SAFETY", "REGULAR_FILES", "ARTIFACT_SET", "BYTE_LENGTH",
        "SHA256", "MASTER_STRUCTURE", "MASTER_FULL_DECODE", "TIMING_EVENTS", "METADATA_PROFILE", "FINALIZATION"
    })
    {
        var required = name is not ("MASTER_STRUCTURE" or "MASTER_FULL_DECODE" or "METADATA_PROFILE");
        checks.Add(new Dictionary<string, object?>
        {
            ["check"] = name,
            ["required"] = required,
            ["disposition"] = required ? "PASS" : "NOT_APPLICABLE",
            ["evidence_references"] = new List<object?>()
        });
    }

    var verification = new Dictionary<string, object?>
    {
        ["schema_version"] = "1.0.0",
        ["verification_record_id"] = verificationId.ToString("D"),
        ["revision"] = 1,
        ["package_id"] = packageId.ToString("D"),
        ["package_content_sha256"] = packageContentHash,
        ["artifact_set_sha256"] = artifactSetHash,
        ["verified_by_windows_account"] = "TEST\\operator",
        ["verifier_name"] = "HumCaptureVerifier",
        ["verifier_version"] = "1.0.0",
        ["verified_utc"] = "2026-09-10T11:01:00.000Z",
        ["outcome"] = "VERIFIED",
        ["checks"] = checks,
        ["artifact_results"] = new List<object?>
        {
            ArtifactResult(eventArtifactId, "events.json", eventBytes.Length, eventHash),
            ArtifactResult(finalizationArtifactId, "finalization.json", finalizationBytes.Length, finalizationHash)
        }
    };
    verificationEdit?.Invoke(verification);
    var verificationBytes = JsonSerializer.SerializeToUtf8Bytes(verification, new JsonSerializerOptions { WriteIndented = true });
    var registration = new StagedVerifiedPackageRegistration
    {
        TransactionId = transactionId,
        TransitionId = transitionId,
        OperationId = operationId,
        RecordIndexEntryId = indexId,
        SubjectId = subjectId,
        SessionId = sessionId,
        TrialId = trialId,
        SourceId = sourceId,
        CaptureAttemptId = captureAttemptId,
        CollectionAttemptId = collectionAttemptId,
        PackageId = packageId,
        PackageContentSha256 = packageContentHash,
        ArtifactSetSha256 = artifactSetHash,
        VerificationRecordId = verificationId,
        VerificationRecordContentSha256 = Convert.ToHexStringLower(SHA256.HashData(verificationBytes)),
        PackageByteLength = packageByteLength,
        ArtifactCount = artifacts.Count,
        ActorWindowsAccount = "TEST\\operator",
        VerificationRecordUtf8 = verificationBytes,
        RecordedAt = new DateTimeOffset(2026, 9, 10, 11, variant, 0, TimeSpan.Zero)
    };
    return (registration, stagingRelativePath, destinationRelativePath, verificationRelativePath);

    Dictionary<string, object?> Artifact(Guid id, string path, string role, int length, string hash) => new()
    {
        ["artifact_id"] = id.ToString("D"), ["relative_path"] = path, ["role"] = role,
        ["media_type"] = "application/json", ["byte_length"] = length.ToString(), ["sha256"] = hash,
        ["required"] = true, ["session_id"] = sessionId.ToString("D"), ["trial_id"] = trialId.ToString("D"),
        ["source_id"] = sourceId.ToString("D"), ["capture_attempt_id"] = captureAttemptId.ToString("D")
    };
    static Dictionary<string, object?> ArtifactResult(Guid id, string path, int length, string hash) => new()
    {
        ["artifact_id"] = id.ToString("D"), ["relative_path"] = path, ["required"] = true,
        ["expected_byte_length"] = length.ToString(), ["observed_byte_length"] = length.ToString(),
        ["expected_sha256"] = hash, ["observed_sha256"] = hash, ["disposition"] = "PASS"
    };
}

static Guid TestId(int value) => Guid.Parse($"10000000-0000-4000-8000-{value:D12}");

static string CanonicalHash(Dictionary<string, object?> value, string excludedProperty)
{
    using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value));
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
    {
        WriteCanonical(writer, document.RootElement, excludedProperty);
    }
    return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
}

static void WriteCanonical(Utf8JsonWriter writer, JsonElement element, string? excludedProperty = null)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().Where(property => property.Name != excludedProperty).OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
            break;
        case JsonValueKind.Array:
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                WriteCanonical(writer, item);
            }
            writer.WriteEndArray();
            break;
        case JsonValueKind.String: writer.WriteStringValue(element.GetString()); break;
        case JsonValueKind.Number: writer.WriteNumberValue(element.GetInt64()); break;
        case JsonValueKind.True: writer.WriteBooleanValue(true); break;
        case JsonValueKind.False: writer.WriteBooleanValue(false); break;
        case JsonValueKind.Null: writer.WriteNullValue(); break;
        default: throw new InvalidOperationException("Unsupported fixture JSON value.");
    }
}

static long ScalarLong(SqliteConnection connection, string sql)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    return (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Expected scalar result."));
}

static void AssertNoJournalRows(string root)
{
    using var connection = OpenCatalog(root, SqliteOpenMode.ReadOnly);
    Equal(0L, ScalarLong(connection, "SELECT count(*) FROM repository_transactions"));
    Equal(0L, ScalarLong(connection, "SELECT count(*) FROM repository_transitions"));
    Equal(0L, ScalarLong(connection, "SELECT count(*) FROM repository_record_index"));
}

static void WithRepository(Action<string, RepositoryService> action)
{
    WithTemporaryRoot(root =>
    {
        var service = new RepositoryService();
        var result = service.Initialize(
            root,
            "TEST\\operator",
            new DateTimeOffset(2026, 9, 10, 10, 30, 0, TimeSpan.Zero),
            Guid.Parse("10000000-0000-4000-8000-000000000001"));
        Equal(RepositoryAccessMode.MutationAllowed, result.AccessMode);
        action(root, service);
    });
}

static void WithTemporaryRoot(Action<string> action)
{
    var root = Path.Combine(Path.GetTempPath(), $"humcapture-repository-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        action(root);
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static SqliteConnection OpenCatalog(string root, SqliteOpenMode mode)
{
    var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(root, "catalog", "humcapture.sqlite3"),
        Mode = mode,
        Pooling = false
    }.ConnectionString);
    connection.Open();
    return connection;
}

static void CreateHardLink(string linkPath, string existingPath)
{
    var startInfo = new ProcessStartInfo("fsutil.exe")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    startInfo.ArgumentList.Add("hardlink");
    startInfo.ArgumentList.Add("create");
    startInfo.ArgumentList.Add(linkPath);
    startInfo.ArgumentList.Add(existingPath);
    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start fsutil hardlink test helper.");
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Could not create test hard link: {process.StandardError.ReadToEnd()}");
    }
}

static void AssertReadOnlyWithoutCatalogWrite(string root, RepositoryService service, string reason)
{
    var catalog = Path.Combine(root, "catalog", "humcapture.sqlite3");
    var hash = Hash(catalog);
    var lastWrite = File.GetLastWriteTimeUtc(catalog);
    var result = service.Open(root);
    Equal(RepositoryAccessMode.ReadOnlyInspection, result.AccessMode);
    Equal(reason, result.ReasonCode);
    Equal(hash, Hash(catalog));
    Equal(lastWrite, File.GetLastWriteTimeUtc(catalog));
}

static void EditDescriptor(string root, Action<Dictionary<string, object?>> edit)
{
    var path = Path.Combine(root, "repository.json");
    var values = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllBytes(path))!;
    foreach (var key in values.Keys.ToArray())
    {
        if (values[key] is JsonElement element)
        {
            values[key] = ConvertElement(element);
        }
    }
    edit(values);
    File.WriteAllText(path, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
}

static object? ConvertElement(JsonElement element) => element.ValueKind switch
{
    JsonValueKind.Array => element.EnumerateArray().Select(ConvertElement).ToList(),
    JsonValueKind.String => element.GetString(),
    JsonValueKind.Number => element.GetInt64(),
    JsonValueKind.True => true,
    JsonValueKind.False => false,
    JsonValueKind.Null => null,
    _ => throw new InvalidOperationException($"Unexpected fixture value kind {element.ValueKind}.")
};

static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

static void Throws(RepositoryErrorCode expected, Action action)
{
    try
    {
        action();
    }
    catch (RepositoryException exception) when (exception.Code == expected)
    {
        return;
    }

    throw new InvalidOperationException($"Expected RepositoryException code {expected}.");
}

static void True(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("Expected true.");
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
    }
}
