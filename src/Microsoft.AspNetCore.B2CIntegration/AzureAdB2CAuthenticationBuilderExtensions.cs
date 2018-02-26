﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.using Microsoft.AspNetCore.Authorization;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.B2CIntegration;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication
{
    /// <summary>
    /// Extension methods to add B2C Authentication to your application.
    /// </summary>
    public static class AzureAdB2CAuthenticationBuilderExtensions
    {
        /// <summary>
        /// Adds B2C Authentication to your application.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
        /// <param name="configureOptions">The <see cref="Action{AzureAdB2COptions}"/> to configure the
        /// <see cref="AzureAdB2COptions"/>
        /// </param>
        /// <returns>The <see cref="AuthenticationBuilder"/>.</returns>
        public static AuthenticationBuilder AddAzureAdB2C(this AuthenticationBuilder builder, Action<AzureAdB2COptions> configureOptions) =>
            builder.AddAzureAdB2C(
                AzureAdB2CDefaults.AuthenticationScheme,
                AzureAdB2CDefaults.OpenIdScheme,
                AzureAdB2CDefaults.CookieScheme,
                AzureAdB2CDefaults.DisplayName,
                configureOptions);

        /// <summary>
        /// Adds B2C Authentication to your application.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
        /// <param name="configureOptions">The <see cref="Action{AzureAdB2COptions}"/> to configure the
        /// <see cref="AzureAdB2COptions"/>
        /// </param>
        /// <returns>The <see cref="AuthenticationBuilder"/>.</returns>
        public static AuthenticationBuilder AddAzureAdB2C(
            this AuthenticationBuilder builder,
            string scheme,
            string openIdConnectScheme,
            string cookieScheme,
            string displayName,
            Action<AzureAdB2COptions> configureOptions)
        {
            AddAdditionalMvcApplicationParts(builder.Services);
            builder.AddVirtualScheme(scheme, displayName, o =>
            {
                o.Default = cookieScheme;
                o.Challenge = openIdConnectScheme;
            });

            builder.Services.Configure<AzureAdB2CSchemeOptions>(o =>
                o.Mappings.Add(new AzureAdB2CSchemeOptions.B2CSchemeMapping
                {
                    B2CScheme = scheme,
                    OpenIdConnectScheme = openIdConnectScheme,
                    CookieScheme = cookieScheme
                }));

            builder.Services.TryAddSingleton<IConfigureOptions<AzureAdB2COptions>, B2COptionsConfiguration>();

            builder.Services.TryAddSingleton<IConfigureOptions<OpenIdConnectOptions>, OpenIdConnectOptionsConfiguration>();

            builder.Services.Configure(scheme, configureOptions);

            builder.AddOpenIdConnect(openIdConnectScheme, null, o => { });
            builder.AddCookie(cookieScheme, null, o => { });

            return builder;
        }

        private static void AddAdditionalMvcApplicationParts(IServiceCollection services)
        {
            var thisAssembly = typeof(AzureAdB2CAuthenticationBuilderExtensions).Assembly;
            var additionalReferences = thisAssembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(am => string.Equals(am.Key, "Microsoft.AspNetCore.Mvc.AdditionalReference"))
                .Select(am => am.Value.Split(',')[0])
                .ToArray();

            var mvcBuilder = services
                .AddMvc()
                .ConfigureApplicationPartManager(apm =>
                {
                    foreach (var reference in additionalReferences)
                    {
                        var fileName = Path.GetFileName(reference);
                        var filePath = Path.Combine(Path.GetDirectoryName(thisAssembly.Location), fileName);
                        var additionalAssembly = LoadAssembly(filePath);
                        // This needs to change to additional assembly part.
                        var additionalPart = new AdditionalAssemblyPart(additionalAssembly);
                        if (!apm.ApplicationParts.Any(ap => HasSameName(ap.Name, additionalPart.Name)))
                        {
                            apm.ApplicationParts.Add(additionalPart);
                        }
                    }

                    apm.FeatureProviders.Add(new B2CAccountControllerFeatureProvider());
                });

            bool HasSameName(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
        }

        private static Assembly LoadAssembly(string filePath)
        {
            Assembly viewsAssembly = null;
            if (File.Exists(filePath))
            {
                try
                {
                    viewsAssembly = Assembly.LoadFile(filePath);
                }
                catch (FileLoadException)
                {
                    throw new InvalidOperationException("Unable to load the precompiled views assembly in " +
                        $"'{filePath}'.");
                }
            }
            else
            {
                throw new InvalidOperationException("Could not find the precompiled views assembly for 'Microsoft.AspNetCore.Identity.UI' at " +
                    $"'{filePath}'.");
            }

            return viewsAssembly;
        }
    }
}