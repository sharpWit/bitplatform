﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Contracts;
using Bit.Core.Models;
using Bit.Test;
using Bit.Test.Core.Implementations;
using FakeItEasy;
using IdentityModel.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Owin;

namespace Bit.Tests.Api.Middlewares.Tests
{
    [TestClass]
    public class ExceptionHandlerMiddlewareTests
    {
        [TestMethod]
        [TestCategory("ExceptionHandler"), TestCategory("Logging")]
        public async Task ExceptionHandlerMustNotSaveAnyThingToLogStoreBecauseOfSuccessfulRequests()
        {
            using (BitOwinTestEnvironment testEnvironment = new BitOwinTestEnvironment())
            {
                await testEnvironment.Server.Login("ValidUserName", "ValidPassword", clientId: "TestResOwner");

                IScopeStatusManager scopeStatusManager = TestDependencyManager.CurrentTestDependencyManager
                    .Objects.OfType<IScopeStatusManager>()
                    .Single();

                A.CallTo(() => scopeStatusManager.MarkAsSucceeded())
                    .MustHaveHappened(Repeated.Exactly.Once);
            }
        }

        [TestMethod]
        [TestCategory("ExceptionHandler"), TestCategory("Logging")]
        public async Task ExceptionHandlerMustSaveExceptionToLogStoreBecauseOfExceptionInRequest()
        {
            using (BitOwinTestEnvironment testEnvironment = new BitOwinTestEnvironment(new TestEnvironmentArgs
            {
                AdditionalDependencies = manager =>
                {
                    manager.RegisterOwinMiddlewareUsing(owinApp =>
                    {
                        owinApp.Map("/Exception", innerApp =>
                        {
                            innerApp.Use<ExceptionThrownMiddleware>();
                        });
                    });
                }
            }))
            {
                try
                {
                    TokenResponse token = await testEnvironment.Server.Login("ValidUserName", "ValidPassword", clientId: "TestResOwner");

                    await testEnvironment.Server.BuildHttpClient(token)
                        .GetAsync("/Exception");

                    Assert.Fail();
                }
                catch
                {
                    IScopeStatusManager scopeStatusManager = TestDependencyManager.CurrentTestDependencyManager
                        .Objects.OfType<IScopeStatusManager>()
                        .Last();

                    A.CallTo(() => scopeStatusManager.MarkAsFailed())
                        .MustHaveHappened(Repeated.Exactly.Once);

                    ILogger logger = TestDependencyManager.CurrentTestDependencyManager
                        .Objects.OfType<ILogger>()
                        .Last();

                    A.CallTo(() => logger.LogExceptionAsync(A<Exception>.That.Matches(e => e is InvalidOperationException), A<string>.Ignored))
                        .MustHaveHappened(Repeated.Exactly.Once);

                    LogData[] logData = logger.LogData.ToArray();

                    logData.Single(c => c.Key == "ExceptionType" && ((string)c.Value).Contains("InvalidOperationException"));
                    logData.Single(c => c.Key == nameof(IRequestInformationProvider.HttpMethod) && (string)c.Value == "GET");
                    logData.Single(c => c.Key == "UserId" && (string)c.Value == "ValidUserName");
                }
            }
        }

        [TestMethod]
        [TestCategory("ExceptionHandler"), TestCategory("Logging")]
        public async Task ExceptionHandlerMustSaveFatalToLogStoreBecauseOfErrorRelatedToServer()
        {
            using (BitOwinTestEnvironment testEnvironment = new BitOwinTestEnvironment(new TestEnvironmentArgs
            {
                AdditionalDependencies = manager =>
                {
                    manager.RegisterOwinMiddlewareUsing(owinApp =>
                    {
                        owinApp.Map("/InternalServerError", innerApp =>
                        {
                            innerApp.Use<InternalServerErrorMiddleware>();
                        });
                    });
                }
            }))
            {
                TokenResponse token = await testEnvironment.Server.Login("ValidUserName", "ValidPassword", clientId: "TestResOwner");

                await testEnvironment.Server.BuildHttpClient(token)
                            .GetAsync("/InternalServerError");

                IScopeStatusManager scopeStatusManager = TestDependencyManager.CurrentTestDependencyManager
                    .Objects.OfType<IScopeStatusManager>()
                    .Last();

                A.CallTo(() => scopeStatusManager.MarkAsFailed())
                            .MustHaveHappened(Repeated.Exactly.Once);

                ILogger logger = TestDependencyManager.CurrentTestDependencyManager
                    .Objects.OfType<ILogger>()
                    .Last();

                A.CallTo(() => logger.LogFatalAsync(A<string>.Ignored))
                            .MustHaveHappened(Repeated.Exactly.Once);

                LogData[] logData = logger.LogData.ToArray();

                logData.Single(c => c.Key == "ResponseStatusCode" && (string)c.Value == "501");
                logData.Single(c => c.Key == nameof(IRequestInformationProvider.HttpMethod) && (string)c.Value == "GET");
                logData.Single(c => c.Key == "UserId" && (string)c.Value == "ValidUserName");
            }
        }

        [TestMethod]
        [TestCategory("ExceptionHandler"), TestCategory("Logging")]
        public async Task ExceptionHandlerMustSaveWarningToLogStoreBecauseOfErrorRelatedToClientRequest()
        {
            using (BitOwinTestEnvironment testEnvironment = new BitOwinTestEnvironment())
            {
                TokenResponse token = await testEnvironment.Server.Login("ValidUserName", "ValidPassword", clientId: "TestResOwner");

                await testEnvironment.Server.BuildHttpClient(token)
                    .GetAsync("/odata/Test/XYZ");

                IScopeStatusManager scopeStatusManager = TestDependencyManager.CurrentTestDependencyManager
                    .Objects.OfType<IScopeStatusManager>()
                    .Last();

                A.CallTo(() => scopeStatusManager.MarkAsFailed())
                    .MustHaveHappened(Repeated.Exactly.Once);

                ILogger logger = TestDependencyManager.CurrentTestDependencyManager
                    .Objects.OfType<ILogger>()
                    .Last();

                A.CallTo(() => logger.LogWarningAsync(A<string>.Ignored))
                    .MustHaveHappened(Repeated.Exactly.Once);

                LogData[] logData = logger.LogData.ToArray();

                logData.Single(c => c.Key == "ResponseStatusCode" && (string)c.Value == "406");
                logData.Single(c => c.Key == nameof(IRequestInformationProvider.HttpMethod) && (string)c.Value == "GET");
                logData.Single(c => c.Key == "UserId" && (string)c.Value == "ValidUserName");
            }
        }
    }
}
