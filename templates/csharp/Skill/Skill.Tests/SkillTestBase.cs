﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Claims;
using System.Threading;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Solutions;
using Microsoft.Bot.Solutions.Responses;
using Microsoft.Bot.Solutions.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using $ext_safeprojectname$.Bots;
using $ext_safeprojectname$.Dialogs;
using $ext_safeprojectname$.Services;
using $safeprojectname$.Utilities;

namespace $safeprojectname$
{
    public class SkillTestBase : BotTestBase
    {
        public IServiceCollection Services { get; set; }

        public LocaleTemplateEngineManager TemplateEngine { get; set; }

        [TestInitialize]
        public virtual void InitializeSkill()
        {
            Services = new ServiceCollection();
            Services.AddSingleton(new BotSettings());
            Services.AddSingleton(new BotServices()
            {
                CognitiveModelSets = new Dictionary<string, CognitiveModelSet>
                {
                    {
                        "en-us", new CognitiveModelSet
                        {
                            LuisServices = new Dictionary<string, LuisRecognizer>
                            {
                                { "General", GeneralTestUtil.CreateRecognizer() },
                                { "$ext_safeprojectname$", SkillTestUtil.CreateRecognizer() }
                            }
                        }
                    }
                }
            });

            Services.AddSingleton<IBotTelemetryClient, NullBotTelemetryClient>();
            Services.AddSingleton(new MicrosoftAppCredentials("appId", "password"));
            Services.AddSingleton(new UserState(new MemoryStorage()));
            Services.AddSingleton(new ConversationState(new MemoryStorage()));
            Services.AddSingleton(sp =>
            {
                var userState = sp.GetService<UserState>();
                var conversationState = sp.GetService<ConversationState>();
                return new BotStateSet(userState, conversationState);
            });

            var localizedTemplates = new Dictionary<string, List<string>>();
            var templateFiles = new List<string>() { "MainResponses", "SampleResponses" };
            var supportedLocales = new List<string>() { "en-us", "de-de", "es-es", "fr-fr", "it-it", "zh-cn" };

            foreach (var locale in supportedLocales)
            {
                var localeTemplateFiles = new List<string>();
                foreach (var template in templateFiles)
                {
                    // LG template for en-us does not include locale in file extension.
                    if (locale.Equals("en-us"))
                    {
                        localeTemplateFiles.Add(Path.Combine(".", "Responses", $"{template}.lg"));
                    }
                    else
                    {
                        localeTemplateFiles.Add(Path.Combine(".", "Responses", $"{template}.{locale}.lg"));
                    }
                }

                localizedTemplates.Add(locale, localeTemplateFiles);
            }

            TemplateEngine = new LocaleTemplateEngineManager(localizedTemplates, "en-us");
            System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-us");
            Services.AddSingleton(TemplateEngine);
            Services.AddTransient<MainDialog>();
            Services.AddTransient<SampleDialog>();
            Services.AddTransient<SampleAction>();
            Services.AddSingleton<TestAdapter, DefaultTestAdapter>();
            Services.AddTransient<IBot, DefaultActivityHandler<MainDialog>>();
        }

        public TestFlow GetTestFlow()
        {
            var sp = Services.BuildServiceProvider();
            var adapter = sp.GetService<TestAdapter>();

            var testFlow = new TestFlow(adapter, async (context, token) =>
            {
                var bot = sp.GetService<IBot>();
                await bot.OnTurnAsync(context, CancellationToken.None);
            });

            return testFlow;
        }

        public TestFlow GetSkillTestFlow()
        {
            var sp = Services.BuildServiceProvider();
            var adapter = sp.GetService<TestAdapter>();

            var testFlow = new TestFlow(adapter, async (context, token) =>
            {
                // Set claims in turn state to simulate skill mode
                var claims = new List<Claim>();
                claims.Add(new Claim(AuthenticationConstants.VersionClaim, "1.0"));
                claims.Add(new Claim(AuthenticationConstants.AudienceClaim, Guid.NewGuid().ToString()));
                claims.Add(new Claim(AuthenticationConstants.AppIdClaim, Guid.NewGuid().ToString()));
                context.TurnState.Add("BotIdentity", new ClaimsIdentity(claims));

                var bot = sp.GetService<IBot>();
                await bot.OnTurnAsync(context, CancellationToken.None);
            });

            return testFlow;
        }

        public string[] GetTemplates(string name, object data = null)
        {
            return TemplateEngine.TemplateEnginesPerLocale[CultureInfo.CurrentUICulture.Name].ExpandTemplate(name, data).ToArray();
        }
    }
}