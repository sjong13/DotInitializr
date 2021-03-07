﻿/*
Copyright 2020 Dicky Suryadi

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DynamicExpresso;

namespace DotInitializr
{
   /// <summary>
   /// Reads metadata from a project template.
   /// </summary>
   public interface ITemplateMetadataReader
   {
      TemplateMetadata GetMetadata(AppConfiguration.Template template);

      Dictionary<string, bool> GetComputedTags(TemplateMetadata metadata, Dictionary<string, object> tagValues);

      Dictionary<string, bool> GetConditionalTags(TemplateMetadata metadata);

      Dictionary<string, string> GetTags(TemplateMetadata metadata);

      Dictionary<string, string> GetTagRegexes(TemplateMetadata metadata);

      string GetFilesToExclude(TemplateMetadata metadata, Dictionary<string, bool> conditionalTags);
   }

   public class TemplateMetadataReader : ITemplateMetadataReader
   {
      public const string PROJECT_NAME_KEY = "projectName";
      public const string PROJECT_NAME = "Project Name";
      public const string DEFAULT_PROJECT_NAME = "Starter";

      private readonly IEnumerable<ITemplateSource> _templateSources;

      private delegate int CountDelegate(params bool[] tags);

      public TemplateMetadataReader(IEnumerable<ITemplateSource> templateSources)
      {
         _templateSources = templateSources;
      }

      public TemplateMetadata GetMetadata(AppConfiguration.Template template)
      {
         TemplateMetadata metadata = null;

         var metadataFile = GetMetadataFile(template);
         if (metadataFile != null && !string.IsNullOrEmpty(metadataFile.Content))
         {
            try
            {
               if (metadataFile.Name.EndsWith(DotNetTemplateMetadata.FILE_NAME, StringComparison.InvariantCultureIgnoreCase))
                  metadata = new DotNetTemplateMetadataMapper().Map(DotNetTemplateMetadata.FromJson(metadataFile.Content));
               else
                  metadata = JsonSerializer.Deserialize<TemplateMetadata>(metadataFile.Content);
            }
            catch (Exception ex)
            {
               var error = $"`{metadataFile.Name}` in `{template.SourceUrl}` must be in JSON";
               Console.WriteLine(error + Environment.NewLine + ex.ToString());
               throw new TemplateException(error);
            }

            // Make sure the tags have keys. Names can be used to substitute keys.
            if (metadata.Tags != null)
            {
               foreach (var tag in metadata.Tags.Where(x => string.IsNullOrEmpty(x.Key)))
                  tag.Key = tag.Name;
               metadata.Tags = metadata.Tags.Where(x => !string.IsNullOrEmpty(x.Key));
            }

            if (metadata.ConditionalTags != null)
            {
               foreach (var tag in metadata.ConditionalTags.Where(x => string.IsNullOrEmpty(x.Key)))
                  tag.Key = tag.Name;
               metadata.ConditionalTags = metadata.ConditionalTags.Where(x => !string.IsNullOrEmpty(x.Key));
            }

            if (metadata.ComputedTags != null)
               metadata.ComputedTags = metadata.ComputedTags.Where(x => !string.IsNullOrEmpty(x.Key));
         }

         return metadata;
      }

      private TemplateFile GetMetadataFile(AppConfiguration.Template template)
      {
         TemplateFile metadataFile = null;

         var templateSource = _templateSources.FirstOrDefault(x => string.Equals(x.SourceType, template?.SourceType, StringComparison.InvariantCultureIgnoreCase));
         if (templateSource != null)
         {
            try
            {
               metadataFile = templateSource.GetFile(TemplateMetadata.FILE_NAME, template.SourceUrl, template.SourceDirectory);
            }
            catch (TemplateException) { }

            if (metadataFile == null)
               metadataFile = templateSource.GetFile("template.json", template.SourceUrl, template.SourceDirectory + "/.template.config");
         }

         return metadataFile;
      }

      public Dictionary<string, bool> GetComputedTags(TemplateMetadata metadata, Dictionary<string, object> tagValues)
      {
         var result = new Dictionary<string, bool>();

         if (metadata.ComputedTags != null)
         {
            var interpreter = new Interpreter();
            foreach (var tag in tagValues)
               interpreter.SetVariable(tag.Key, tag.Value);

            CountDelegate countFunc = Count;
            interpreter.SetFunction("Count", countFunc);

            foreach (var computedTag in metadata.ComputedTags)
            {
               try
               {
                  object value = interpreter.Eval(computedTag.Expression);
                  if (value is bool)
                     result.Add(computedTag.Key, (bool) value == true);
               }
               catch (Exception ex)
               {
                  var error = $"Cannot compute `{computedTag.Key}` expression `{computedTag.Expression}`";
                  Console.WriteLine(error + Environment.NewLine + ex.ToString());
                  throw new TemplateException(error);
               }
            }
         }

         return result;
      }

      public Dictionary<string, string> GetTags(TemplateMetadata metadata)
      {
         var result = new Dictionary<string, string>();

         if (metadata?.Tags != null)
            foreach (var tag in metadata.Tags)
               result.Add(tag.Key, tag.DefaultValue);

         return result;
      }

      public Dictionary<string, string> GetTagRegexes(TemplateMetadata metadata)
      {
         var result = new Dictionary<string, string>();

         if (metadata?.Tags != null)
            foreach (var tag in metadata.Tags.Where(x => !string.IsNullOrWhiteSpace(x.Regex)))
               result.Add(tag.Key, tag.Regex);

         return result;
      }

      public Dictionary<string, bool> GetConditionalTags(TemplateMetadata metadata)
      {
         var result = new Dictionary<string, bool>();

         if (metadata?.ConditionalTags != null)
            foreach (var tag in metadata.ConditionalTags)
               result.Add(tag.Key, tag.DefaultValue ?? false);

         return result;
      }

      public string GetFilesToExclude(TemplateMetadata metadata, Dictionary<string, bool> tags)
      {
         string result = "";

         if (metadata.ConditionalTags != null)
            foreach (var tag in metadata.ConditionalTags.Where(x => !tags.ContainsKey(x.Key) || !tags[x.Key]))
               result = string.Join(',', result, tag.FilesToInclude);

         if (metadata.ComputedTags != null)
            foreach (var tag in metadata.ComputedTags.Where(x => !tags.ContainsKey(x.Key) || !tags[x.Key]))
               result = string.Join(',', result, tag.FilesToInclude);

         return result.Trim(',');
      }

      private int Count(params bool[] tags) => tags.Where(x => x).Count();
   }
}