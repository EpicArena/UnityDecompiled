﻿// Decompiled with JetBrains decompiler
// Type: UnityEngine.ParticleRenderMode
// Assembly: UnityEngine, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D290425A-E4B3-4E49-A420-29F09BB3F974
// Assembly location: C:\Program Files\Unity 5\Editor\Data\Managed\UnityEngine.dll

using System;

namespace UnityEngine
{
  [Obsolete("This is part of the legacy particle system, which is deprecated and will be removed in a future release. Use the ParticleSystem component instead.", false)]
  public enum ParticleRenderMode
  {
    Billboard = 0,
    SortedBillboard = 2,
    Stretch = 3,
    HorizontalBillboard = 4,
    VerticalBillboard = 5,
  }
}