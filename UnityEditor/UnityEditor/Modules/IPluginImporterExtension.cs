﻿// Decompiled with JetBrains decompiler
// Type: UnityEditor.Modules.IPluginImporterExtension
// Assembly: UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53BAA40C-AA1D-48D3-AA10-3FCF36D212BC
// Assembly location: C:\Program Files\Unity 5\Editor\Data\Managed\UnityEditor.dll

namespace UnityEditor.Modules
{
  internal interface IPluginImporterExtension
  {
    void ResetValues(PluginImporterInspector inspector);

    bool HasModified(PluginImporterInspector inspector);

    void Apply(PluginImporterInspector inspector);

    void OnEnable(PluginImporterInspector inspector);

    void OnDisable(PluginImporterInspector inspector);

    void OnPlatformSettingsGUI(PluginImporterInspector inspector);

    string CalculateFinalPluginPath(string buildTargetName, PluginImporter imp);

    bool CheckFileCollisions(string buildTargetName);
  }
}