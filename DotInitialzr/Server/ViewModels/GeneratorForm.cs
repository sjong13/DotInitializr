﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DotInitialzr.Shared;
using DotNetify;
using DotNetify.Elements;

namespace DotInitialzr.Server
{
   public class GeneratorForm : BaseVM
   {
      private readonly MetadataForm _metadataForm;
      private readonly AppConfiguration _config;
      private Dictionary<string, object> _metadata;

      public bool Loading { get => Get<bool>(); set => Set(value); }

      public GeneratorForm(AppConfiguration config, MetadataForm metadataForm)
      {
         _config = config;
         _metadataForm = metadataForm;
         _metadataForm.MetadataLoadedEvent.Subscribe(_ => Loading = false);

         AddProperty("Template", "none")
            .WithAttribute(new DropdownListAttribute
            {
               Label = "Template",
               Placeholder = "Select one...",
               Options = config.Templates.Select(x => KeyValuePair.Create(x.Key, x.Name)).Prepend(KeyValuePair.Create("none", "")).ToArray()
            })
            .WithServerValidation(x => true, string.Empty)  // Add this so that input field change is dispatched to the server VM.
            .SubscribedBy(_metadataForm.TemplateChangedEvent, key =>
            {
               Loading = true;
               PushUpdates();

               _metadata = null;
               return _config.Templates.FirstOrDefault(x => x.Key == key);
            })
            .SubscribedBy(AddProperty<string>("Description"), key => _config.Templates.FirstOrDefault(x => x.Key == key)?.Description);

         AddInternalProperty<Dictionary<string, string>>("Generate")
            .WithAttribute(new { Label = "Generate" })
            .Merge(AddInternalProperty<Dictionary<string, string>>(nameof(IGeneratorFormState.ClearProjectMetadata)))
            .Select(formData => formData != null ? BuildProjectMetadata(formData) : null)
            .Subscribe(AddProperty<ProjectMetadata>(nameof(IGeneratorFormState.ProjectMetadata)));
      }

      public override BaseVM GetSubVM(string vmTypeName)
      {
         if (vmTypeName == nameof(MetadataForm))
            return _metadataForm;

         return base.GetSubVM(vmTypeName);
      }

      private ProjectMetadata BuildProjectMetadata(Dictionary<string, string> formData)
      {
         var template = _config.Templates.FirstOrDefault(x => x.Key == formData["Template"]);
         if (template == null)
            return null;

         _metadata ??= _metadataForm.GetDefaultMetadataValues();
         foreach (var key in formData.Keys)
            _metadata[key] = formData[key];

         return new ProjectMetadata
         {
            ProjectName = _metadata[MetadataForm.ProjectNameKey].ToString(),
            TemplateSourceType = template.SourceType,
            TemplateSourceUrl = template.SourceUrl,
            TemplateSourceDirectory = template.SourceDirectory,
            Tags = _metadata
         };
      }
   }
}