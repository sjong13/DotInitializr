﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DotInitializr.Server
{
   public class DotNetRenderer : ITemplateRenderer
   {
      public const string TEMPLATE_TYPE = "dotnet";
      private const string LOWER_CASE = "__lower";
      private const string UPPER_CASE = "__upper";

      public string TemplateType => TEMPLATE_TYPE;

      public IEnumerable<TemplateFile> Render(IEnumerable<TemplateFile> files, Dictionary<string, object> tags)
      {
         var filesWithFormat = files.Where(x => x.Content.Contains(LOWER_CASE) || x.Content.Contains(UPPER_CASE) || x.Name.Contains(LOWER_CASE) || x.Name.Contains(UPPER_CASE));
         foreach (var tag in tags.Where(x => x.Value is string && filesWithFormat.Any(y => y.Content.Contains($"{x.Key}__") || y.Name.Contains($"{x.Key}__"))).ToArray())
         {
            tags.Add($"{tag.Key}{LOWER_CASE}", tag.Value.ToString().ToLowerInvariant());
            tags.Add($"{tag.Key}{UPPER_CASE}", tag.Value.ToString().ToUpperInvariant());
         }

         return files.Select(x =>
         {
            if (tags != null)
            {
               foreach (var tag in tags.Where(x => x.Value is string).OrderByDescending(x => x.Key.Length))
               {
                  if (x.Name.Contains(tag.Key))
                     x.Name = x.Name.Replace(tag.Key, tag.Value.ToString());

                  if (x.Content.Contains(tag.Key))
                     x.Content = x.Content.Replace(tag.Key, tag.Value.ToString());
               }

               foreach (var tag in tags.Where(x => x.Value is bool))
               {
                  if (x.Name.Contains(tag.Key))
                     x.Name = x.Name.Replace(tag.Key, string.Empty);

                  Regex regex = new Regex($"#if {tag.Key}(.*?)#endif", RegexOptions.Singleline);
                  var result = regex.Match(x.Content);
                  if (result.Success)
                  {
                     if ((bool) tag.Value)
                        x.Content = regex.Replace(x.Content, result.Groups[1].Value.Trim('\r', '\n'));
                     else
                        x.Content = regex.Replace(x.Content, string.Empty);
                  }

                  regex = new Regex($"#if !{tag.Key}(.*?)#endif", RegexOptions.Singleline);
                  result = regex.Match(x.Content);
                  if (result.Success)
                  {
                     if ((bool) tag.Value)
                        x.Content = regex.Replace(x.Content, string.Empty);
                     else
                        x.Content = regex.Replace(x.Content, result.Groups[1].Value.Trim('\r', '\n'));
                  }
               }
            }

            return new TemplateFile
            {
               Name = x.Name,
               Content = x.Content
            };
         });
      }
   }
}