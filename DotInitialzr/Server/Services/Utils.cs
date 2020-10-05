﻿using System;
using System.Diagnostics;
using System.IO;

namespace DotInitialzr.Server
{
   public class Utils
   {
      public static void DeleteDirectory(string path)
      {
         try
         {
            foreach (var fileName in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
               var fileInfo = new FileInfo(fileName);
               fileInfo.Attributes = FileAttributes.Normal;
               fileInfo.Delete();
            }

            Directory.Delete(path, true);
         }
         catch (Exception ex)
         {
            Trace.TraceError($"Failed to delete `{path}`: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
         }
      }
   }
}