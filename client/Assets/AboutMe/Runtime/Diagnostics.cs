using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace AboutMe
{
    public class Diagnostics : MonoBehaviour
    {
        readonly List<float> frames=new List<float>(1000);float next=10;
        void Update()
        {
            frames.Add(UnityEngine.Time.unscaledDeltaTime*1000);
            if(UnityEngine.Time.unscaledTime<next)return;next=UnityEngine.Time.unscaledTime+10;
            frames.Sort();float total=0;foreach(float f in frames)total+=f;
            float fps=frames.Count*1000/Mathf.Max(1,total),p95=frames[Mathf.Min(frames.Count-1,Mathf.FloorToInt(frames.Count*.95f))];
            Debug.Log("ABOUTME_PERF fps="+fps.ToString("F1")+" p95_ms="+p95.ToString("F1")+" unity_allocated_mb="+(Profiler.GetTotalAllocatedMemoryLong()/1048576f).ToString("F1"));frames.Clear();
        }
    }
}
