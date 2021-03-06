// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestCommand : DockerRegistryCommand<PublishManifestOptions>
    {
        public PublishManifestCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING MANIFESTS");

            ExecuteWithUser(() =>
            {
                IEnumerable<ImageInfo> multiArchImages = Manifest.Repos
                    .SelectMany(repo => repo.Images)
                    .Where(image => image.SharedTags.Any());
                foreach (ImageInfo image in multiArchImages)
                {
                    string manifest = GenerateManifest(image);

                    Logger.WriteSubheading($"PUBLISHING MANIFEST:{Environment.NewLine}{manifest}");
                    File.WriteAllText("manifest.yml", manifest);

                    // ExecuteWithRetry because the manifest-tool fails periodically while communicating
                    // with the Docker Registry.
                    ExecuteHelper.ExecuteWithRetry("manifest-tool", "push from-spec manifest.yml", Options.IsDryRun);
                }

                WriteManifestSummary(multiArchImages);
            });

            return Task.CompletedTask;
        }

        private string GenerateManifest(ImageInfo image)
        {
            StringBuilder manifestYml = new StringBuilder();
            manifestYml.AppendLine($"image: {image.SharedTags.First().FullyQualifiedName}");

            IEnumerable<string> additionalTags = image.SharedTags
                .Select(tag => tag.Name)
                .Skip(1);
            if (additionalTags.Any())
            {
                manifestYml.AppendLine($"tags: [{string.Join(",", additionalTags)}]");
            }

            manifestYml.AppendLine("manifests:");
            foreach (PlatformInfo platform in image.Platforms)
            {
                manifestYml.AppendLine($"  -");
                manifestYml.AppendLine($"    image: {platform.Tags.First().FullyQualifiedName}");
                manifestYml.AppendLine($"    platform:");
                manifestYml.AppendLine($"      architecture: {platform.Model.Architecture.ToString().ToLowerInvariant()}");
                manifestYml.AppendLine($"      os: {platform.Model.OS.ToString().ToLowerInvariant()}");
                if (platform.Model.Variant != null)
                {
                    manifestYml.AppendLine($"      variant: {platform.Model.Variant}");
                }
            }

            return manifestYml.ToString();
        }

        private void WriteManifestSummary(IEnumerable<ImageInfo> multiArchImages)
        {
            Logger.WriteHeading("MANIFEST TAGS PUBLISHED");

            IEnumerable<string> multiArchTags = multiArchImages.SelectMany(image => image.SharedTags)
                .Select(tag => tag.FullyQualifiedName)
                .ToArray();
            if (multiArchTags.Any())
            {
                foreach (string tag in multiArchTags)
                {
                    Logger.WriteMessage(tag);
                }
            }
            else
            {
                Logger.WriteMessage("No manifests published");
            }

            Logger.WriteMessage();
        }
    }
}
