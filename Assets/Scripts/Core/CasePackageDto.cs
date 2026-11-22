using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    /// <summary>
    /// Key-value metadata pair compatible with JsonUtility.
    /// Used instead of Dictionary for cross-platform serialization safety.
    /// </summary>
    [Serializable]
    public class CaseMetadataEntryDto
    {
        public string key;
        public string value;

        public CaseMetadataEntryDto()
        {
        }

        public CaseMetadataEntryDto(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }

    /// <summary>
    /// Expected command and validation outcome specification for test scenarios.
    /// </summary>
    [Serializable]
    public class ExpectedCommandDto
    {
        public string expectedCommandId;
        public int expectedCommandType;
        public string expectedTargetId;
        public bool expectValid;
        public int expectedFailedGate;
        public string expectedErrorCode;
        public string description;

        public ExpectedCommandDto()
        {
            expectValid = true;
            expectedFailedGate = 0;
            expectedErrorCode = string.Empty;
        }
    }

    /// <summary>
    /// Execution and audit report produced by running a test case package.
    /// Captures the canonical N/M/K/K verification counters required by the gate.
    /// </summary>
    [Serializable]
    public class CasePackageReportDto
    {
        public string packageId;
        public int proposalsCount;
        public int validationRejectedCount;
        public int executionSuccessCount;
        public int readbackConsistentCount;
        public bool isCasePassed;
        public List<string> rejectionReasonList = new List<string>();
        public string executionLog;

        public CasePackageReportDto()
        {
        }

        public CasePackageReportDto(string id, int proposals, int rejected, int executed, int consistent)
        {
            packageId = id;
            proposalsCount = proposals;
            validationRejectedCount = rejected;
            executionSuccessCount = executed;
            readbackConsistentCount = consistent;
            isCasePassed = (executed == consistent) && (proposals == rejected + executed);
        }
    }

    /// <summary>
    /// Complete test scenario case package transfer object.
    /// Bundles prompt text, deterministic mock response, and verification criteria.
    /// </summary>
    [Serializable]
    public class CasePackageDto
    {
        public string packageId;
        public string title;
        public string scenarioPrompt;
        public string mockResponseJson;
        public SceneContextDto initialContext;
        public List<ExpectedCommandDto> expectedCommands = new List<ExpectedCommandDto>();
        public List<CaseMetadataEntryDto> metadata = new List<CaseMetadataEntryDto>();

        public CasePackageDto()
        {
        }

        public CasePackageDto(string id, string title, string prompt, string mockResponse)
        {
            this.packageId = id;
            this.title = title;
            this.scenarioPrompt = prompt;
            this.mockResponseJson = mockResponse;
        }
    }
}
