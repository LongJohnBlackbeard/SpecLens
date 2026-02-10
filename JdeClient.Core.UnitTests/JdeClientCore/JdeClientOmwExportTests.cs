using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Internal;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientOmwExportTests
{
    [Test]
    public async Task ExportProjectToParAsync_UsesObjectInterfacePointerForSave()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.EnvironmentHandle.Returns(new HENV { Handle = new IntPtr(1) });
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(2) });
        TestHelpers.SetupExecuteAsync<JdeOmwExportResult>(session);

        var queryEngine = Substitute.For<IJdeTableQueryEngine>();
        queryEngine.QueryTable(
                "F00942",
                1,
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<string?>())
            .Returns(new JdeQueryResult
            {
                TableName = "F00942",
                Rows = new List<Dictionary<string, object>>
                {
                    new() { ["EMRLS"] = "E920" }
                }
            });
        queryEngine.QueryTable(
                "F98222",
                Arg.Any<int>(),
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<string?>())
            .Returns(new JdeQueryResult { TableName = "F98222" });

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(queryEngine);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"SpecLens_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var expectedPath = Path.Combine(tempRoot, "PRJ_2025-DPT-101_60_99.par");

        var omwApi = new FakeOmwApi
        {
            OnSave = () => File.WriteAllText(expectedPath, "stub")
        };

        var client = new JdeClient(
            session,
            new JdeClientOptions(),
            tableQueryEngineFactory: factory,
            omwApi: omwApi);

        try
        {
            // Act
            var result = await client.ExportProjectToParAsync("2025-DPT-101", "DV920", tempRoot);

            // Assert
            await Assert.That(result.OutputPath).IsEqualTo(Path.GetFullPath(expectedPath));
            await Assert.That(result.FileAlreadyExists).IsFalse();

            var saveCall = omwApi.CallMethodCalls.Single(call => call.Method == JdeOmwMethod.Save);
            var getProjectCall = omwApi.CallMethodCalls.Single(call => call.Method == JdeOmwMethod.GetProject);

            await Assert.That(saveCall.Handle).IsEqualTo(omwApi.ObjectHandle);
            await Assert.That(getProjectCall.Handle).IsEqualTo(omwApi.FactoryHandle);

            await Assert.That(omwApi.GetAttributeCalls.Count).IsEqualTo(1);
            await Assert.That(omwApi.GetAttributeCalls[0].Handle).IsEqualTo(omwApi.ParamHandle);
            await Assert.That(omwApi.GetAttributeCalls[0].Attribute)
                .IsEqualTo(JdeOmwAttribute.OmwParamObjectInterfacePointer);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task ExportProjectToParAsync_MissingObjectInterfacePointer_Throws()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.EnvironmentHandle.Returns(new HENV { Handle = new IntPtr(1) });
        TestHelpers.SetupExecuteAsync<JdeOmwExportResult>(session);

        var queryEngine = Substitute.For<IJdeTableQueryEngine>();
        queryEngine.QueryTable(
                "F00942",
                1,
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<string?>())
            .Returns(new JdeQueryResult
            {
                TableName = "F00942",
                Rows = new List<Dictionary<string, object>>
                {
                    new() { ["EMRLS"] = "E920" }
                }
            });

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(queryEngine);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"SpecLens_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var omwApi = new FakeOmwApi
        {
            ObjectHandle = IntPtr.Zero
        };

        var client = new JdeClient(
            session,
            new JdeClientOptions(),
            tableQueryEngineFactory: factory,
            omwApi: omwApi);

        try
        {
            // Act
            var exception = await Assert.That(async () =>
                    await client.ExportProjectToParAsync("2025-DPT-101", "DV920", tempRoot))
                .ThrowsExactly<JdeApiException>();

            // Assert
            await Assert.That(exception.ApiFunction).IsEqualTo("OMWGetAttribute");
            await Assert.That(omwApi.CallMethodCalls.Any(call => call.Method == JdeOmwMethod.Save)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task ExportProjectToParAsync_NoOutputPath_UsesSaveEx()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(2) });
        TestHelpers.SetupExecuteAsync<JdeOmwExportResult>(session);

        var omwApi = new FakeOmwApi
        {
            SaveExFileExists = true
        };

        var client = new JdeClient(session, new JdeClientOptions(), omwApi: omwApi);

        // Act
        var result = await client.ExportProjectToParAsync("2025-DPT-101", "DV920", outputPath: null);

        // Assert
        await Assert.That(result.OutputPath).IsNull();
        await Assert.That(result.FileAlreadyExists).IsTrue();
        await Assert.That(omwApi.SaveExCalls.Count).IsEqualTo(1);
        await Assert.That(omwApi.SaveExCalls[0].ObjectType).IsEqualTo("PRJ");
    }

    [Test]
    public async Task ExportProjectToParAsync_SavePath_MissingFile_Throws()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.EnvironmentHandle.Returns(new HENV { Handle = new IntPtr(1) });
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(2) });
        TestHelpers.SetupExecuteAsync<JdeOmwExportResult>(session);

        var queryEngine = Substitute.For<IJdeTableQueryEngine>();
        queryEngine.QueryTable(
                "F00942",
                1,
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<string?>())
            .Returns(new JdeQueryResult
            {
                TableName = "F00942",
                Rows = new List<Dictionary<string, object>>
                {
                    new() { ["EMRLS"] = "E920" }
                }
            });

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(queryEngine);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"SpecLens_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var omwApi = new FakeOmwApi();
        var client = new JdeClient(
            session,
            new JdeClientOptions(),
            tableQueryEngineFactory: factory,
            omwApi: omwApi);

        try
        {
            // Act
            var exception = await Assert.That(async () =>
                    await client.ExportProjectToParAsync("2025-DPT-101", "DV920", tempRoot))
                .ThrowsExactly<JdeApiException>();

            // Assert
            await Assert.That(exception.ApiFunction).IsEqualTo("ExportProjectToParAsync");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task ExportProjectToParAsync_AllowsNotSupportedOptionalAttributes()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.EnvironmentHandle.Returns(new HENV { Handle = new IntPtr(1) });
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(2) });
        TestHelpers.SetupExecuteAsync<JdeOmwExportResult>(session);

        var queryEngine = Substitute.For<IJdeTableQueryEngine>();
        queryEngine.QueryTable(
                "F00942",
                1,
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<string?>())
            .Returns(new JdeQueryResult
            {
                TableName = "F00942",
                Rows = new List<Dictionary<string, object>>
                {
                    new() { ["EMRLS"] = "E920" }
                }
            });

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(queryEngine);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"SpecLens_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var expectedPath = Path.Combine(tempRoot, "PRJ_2025-DPT-101_60_99.par");

        var omwApi = new FakeOmwApi
        {
            SetAttributeResults =
            {
                [JdeOmwAttribute.OmwParam7034] = JdeOmwReturn.NotSupported,
                [JdeOmwAttribute.OmwParam7035] = JdeOmwReturn.NotSupported,
                [JdeOmwAttribute.OmwParam7003] = JdeOmwReturn.NotSupported
            },
            OnSave = () => File.WriteAllText(expectedPath, "stub")
        };

        var client = new JdeClient(
            session,
            new JdeClientOptions(),
            tableQueryEngineFactory: factory,
            omwApi: omwApi);

        try
        {
            // Act
            var result = await client.ExportProjectToParAsync("2025-DPT-101", "DV920", tempRoot);

            // Assert
            await Assert.That(result.OutputPath).IsEqualTo(Path.GetFullPath(expectedPath));
            await Assert.That(omwApi.SetAttributeCalls.Count).IsGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task ExportProjectToParAsync_SetAttributeFailure_Throws()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.EnvironmentHandle.Returns(new HENV { Handle = new IntPtr(1) });
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(2) });
        TestHelpers.SetupExecuteAsync<JdeOmwExportResult>(session);

        var queryEngine = Substitute.For<IJdeTableQueryEngine>();
        queryEngine.QueryTable(
                "F00942",
                1,
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<string?>())
            .Returns(new JdeQueryResult
            {
                TableName = "F00942",
                Rows = new List<Dictionary<string, object>>
                {
                    new() { ["EMRLS"] = "E920" }
                }
            });

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(queryEngine);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"SpecLens_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var omwApi = new FakeOmwApi
        {
            SetAttributeResults =
            {
                [JdeOmwAttribute.ObjectType] = JdeOmwReturn.Error
            }
        };

        var client = new JdeClient(
            session,
            new JdeClientOptions(),
            tableQueryEngineFactory: factory,
            omwApi: omwApi);

        try
        {
            // Act
            var exception = await Assert.That(async () =>
                    await client.ExportProjectToParAsync("2025-DPT-101", "DV920", tempRoot))
                .ThrowsExactly<JdeApiException>();

            // Assert
            await Assert.That(exception.ApiFunction).IsEqualTo("OMWSetAttribute");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class FakeOmwApi : IOmwApi
    {
        public readonly List<(IntPtr Handle, JdeOmwMethod Method)> CallMethodCalls = new();
        public readonly List<(string ObjectId, string ObjectType, string PathCode, string ProjectName, bool InsertOnly, int Include64Bit)> SaveExCalls = new();
        public readonly List<(IntPtr Handle, JdeOmwAttribute Attribute)> GetAttributeCalls = new();
        public readonly List<(IntPtr Handle, JdeOmwAttribute Attribute)> SetAttributeCalls = new();
        public readonly List<IntPtr> DeleteObjectCalls = new();

        public readonly Dictionary<JdeOmwAttribute, JdeOmwReturn> SetAttributeResults = new();
        public readonly Dictionary<JdeOmwMethod, JdeOmwReturn> CallMethodResults = new();

        public IntPtr FactoryHandle { get; set; } = new(100);
        public IntPtr ParamHandle { get; set; } = new(200);
        public IntPtr ObjectHandle { get; set; } = new(300);

        public JdeOmwReturn CreateFactoryResult { get; set; } = JdeOmwReturn.Success;
        public JdeOmwReturn CreateParamResult { get; set; } = JdeOmwReturn.Success;
        public JdeOmwReturn SetAttributeResult { get; set; } = JdeOmwReturn.Success;
        public JdeOmwReturn GetAttributeResult { get; set; } = JdeOmwReturn.Success;
        public JdeOmwReturn CallMethodResult { get; set; } = JdeOmwReturn.Success;
        public JdeOmwReturn SaveExResult { get; set; } = JdeOmwReturn.Success;
        public JdeOmwReturn DeleteObjectResult { get; set; } = JdeOmwReturn.Success;
        public bool SaveExFileExists { get; set; }

        public Action? OnSave { get; set; }

        public JdeOmwReturn OMWCreateOMWObjectFactory(HENV hEnv, out IntPtr hObjectFactory)
        {
            hObjectFactory = FactoryHandle;
            return CreateFactoryResult;
        }

        public JdeOmwReturn OMWCreateParamObject(out IntPtr hParam)
        {
            hParam = ParamHandle;
            return CreateParamResult;
        }

        public JdeOmwReturn OMWSetAttribute(
            ref IntPtr hObject,
            JdeOmwAttribute attribute,
            JdeOmwAttrUnion value,
            JdeOmwUnionValue valueType)
        {
            SetAttributeCalls.Add((hObject, attribute));
            return SetAttributeResults.TryGetValue(attribute, out var overrideResult)
                ? overrideResult
                : SetAttributeResult;
        }

        public JdeOmwReturn OMWGetAttribute(IntPtr hObject, JdeOmwAttribute attribute, out JdeOmwAttrUnion value)
        {
            GetAttributeCalls.Add((hObject, attribute));
            value = new JdeOmwAttrUnion { Pointer = ObjectHandle };
            return GetAttributeResult;
        }

        public JdeOmwReturn OMWCallMethod(IntPtr hObject, JdeOmwMethod method, ref IntPtr hParam)
        {
            CallMethodCalls.Add((hObject, method));
            if (method == JdeOmwMethod.Save)
            {
                OnSave?.Invoke();
            }

            return CallMethodResults.TryGetValue(method, out var overrideResult)
                ? overrideResult
                : CallMethodResult;
        }

        public JdeOmwReturn OMWCallSaveObjectToRepositoryEx(
            HUSER hUser,
            string objectId,
            string objectType,
            string pathCode,
            string projectName,
            bool doInsertOnly,
            out bool fileExists,
            int include64BitFiles)
        {
            SaveExCalls.Add((objectId, objectType, pathCode, projectName, doInsertOnly, include64BitFiles));
            fileExists = SaveExFileExists;
            return SaveExResult;
        }

        public JdeOmwReturn OMWDeleteObject(ref IntPtr hObject)
        {
            DeleteObjectCalls.Add(hObject);
            return DeleteObjectResult;
        }
    }
}
