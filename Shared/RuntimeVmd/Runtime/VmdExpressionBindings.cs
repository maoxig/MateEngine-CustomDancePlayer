using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    // Resolve the model's authored expression bindings once. Reflection keeps
    // this shared runtime independent of a particular UniVRM assembly version.
    public sealed class VmdExpressionBindings
    {
        private sealed class Clip
        {
            public string Name, Preset;
            public bool Binary;
            public readonly List<Target> Targets=new List<Target>();
        }
        private sealed class Target { public SkinnedMeshRenderer Renderer;public int Index;public float Weight; }
        private sealed class Term { public string Morph, Group;public float Weight;public bool Binary; }
        private sealed class Binding
        {
            public SkinnedMeshRenderer Renderer;public int Index;public float Original;
            public readonly List<Term> Terms=new List<Term>();
            public readonly Dictionary<string,float> Groups=new Dictionary<string,float>();
        }
        private readonly List<Binding> bindings=new List<Binding>();
        private VmdMotionSampler currentSampler;
        private float currentFrame;
        private readonly Func<string,float?> motionSample;
        public VmdExpressionBindings(){motionSample=SampleMotion;}
        private float? SampleMotion(string name){float value;return currentSampler.TrySampleMorph(name,currentFrame,out value)?(float?)value:null;}
        public int Count { get { return bindings.Count; } }
        public bool HasRenderer(SkinnedMeshRenderer renderer) { return bindings.Exists(b=>b.Renderer==renderer); }
        private static object Read(object value,string name)
        {
            if(value==null)return null;
            var type=value.GetType();var field=type.GetField(name,BindingFlags.Public|BindingFlags.Instance);
            if(field!=null)return field.GetValue(value);
            var property=type.GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
            return property==null?null:property.GetValue(value,null);
        }
        private static string Text(object value){return value==null?"":value.ToString();}
        private static void AddClip(List<Clip> clips,Transform root,object asset,string preset,bool vrm1)
        {
            if(asset==null)return;
            var clip=new Clip{Preset=preset,Name=Text(Read(asset,vrm1?"name":"BlendShapeName")),Binary=Read(asset,"IsBinary") is bool&&(bool)Read(asset,"IsBinary")};
            var values=Read(asset,vrm1?"MorphTargetBindings":"Values") as IEnumerable;
            if(values==null)return;
            foreach(object value in values)
            {
                string path=Text(Read(value,"RelativePath"));var transform=string.IsNullOrEmpty(path)?root:root.Find(path);
                if(transform==null)continue;
                var renderer=transform.GetComponent<SkinnedMeshRenderer>();
                object rawIndex=Read(value,"Index"),rawWeight=Read(value,"Weight");
                if(renderer==null||renderer.sharedMesh==null||rawIndex==null||rawWeight==null)continue;
                int index=Convert.ToInt32(rawIndex);float weight=Convert.ToSingle(rawWeight)*(vrm1?1f:0.01f);
                if(index<0||index>=renderer.sharedMesh.blendShapeCount||float.IsNaN(weight)||float.IsInfinity(weight))continue;
                clip.Targets.Add(new Target{Renderer=renderer,Index=index,Weight=weight});
            }
            if(clip.Targets.Count>0)clips.Add(clip);
        }
        private static List<Clip> ReadClips(Animator animator)
        {
            var clips=new List<Clip>();
            foreach(Component component in animator.GetComponentsInChildren<Component>(true))
            {
                if(component==null)continue;
                string type=component.GetType().FullName;
                if(type=="VRM.VRMBlendShapeProxy")
                {
                    var values=Read(Read(component,"BlendShapeAvatar"),"Clips") as IEnumerable;
                    if(values!=null)foreach(object clip in values)AddClip(clips,component.transform,clip,Text(Read(clip,"Preset")),false);
                }
                if(type=="UniVRM10.Vrm10Instance")
                {
                    var values=Read(Read(Read(component,"Vrm"),"Expression"),"Clips") as IEnumerable;
                    if(values!=null)foreach(object pair in values)AddClip(clips,component.transform,Read(pair,"Item2"),Text(Read(pair,"Item1")),true);
                }
            }
            return clips;
        }
        public static VmdExpressionBindings Create(Animator animator,IEnumerable<string> morphNames)
        {
            var result=new VmdExpressionBindings();if(animator==null)return result;
            var clips=ReadClips(animator);
            foreach(string morph in morphNames)
            foreach(var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=renderer.sharedMesh;if(mesh==null)continue;
                int best=0;var indices=new List<int>();
                for(int i=0;i<mesh.blendShapeCount;i++)
                {
                    int score=VmdMorphNameMatcher.GetMatchScore(mesh.GetBlendShapeName(i),morph);
                    if(score<=0||score<best)continue;
                    if(score>best){best=score;indices.Clear();}indices.Add(i);
                }
                // Exact authored MMD shapes take precedence on this mesh. Other
                // meshes can still use their own VRM bindings (teeth, tongue, etc.).
                if(best>=9500)
                {foreach(int i in indices)result.Add(renderer,i,morph,"direct:"+i,1,false);continue;}
                int clipScore=0;var selected=new List<Clip>();
                foreach(var clip in clips)
                {
                    if(!clip.Targets.Exists(t=>t.Renderer==renderer))continue;
                    int score=Math.Max(VmdMorphNameMatcher.GetMatchScore(clip.Preset,morph),VmdMorphNameMatcher.GetMatchScore(clip.Name,morph));
                    if(score<=0||score<clipScore)continue;
                    if(score>clipScore){clipScore=score;selected.Clear();}selected.Add(clip);
                }
                if(selected.Count>0)
                {
                    foreach(var clip in selected)foreach(var target in clip.Targets)
                        if(target.Renderer==renderer)result.Add(renderer,target.Index,morph,"clip:"+clips.IndexOf(clip),target.Weight,clip.Binary);
                }
                else foreach(int i in indices)result.Add(renderer,i,morph,"direct:"+i,1,false);
            }
            return result;
        }
        private void Add(SkinnedMeshRenderer renderer,int index,string morph,string group,float weight,bool binary)
        {
            var binding=bindings.Find(b=>b.Renderer==renderer&&b.Index==index);
            if(binding==null){binding=new Binding{Renderer=renderer,Index=index,Original=renderer.GetBlendShapeWeight(index)};bindings.Add(binding);}
            if(binding.Terms.Exists(t=>t.Morph==morph&&t.Group==group))return;
            binding.Terms.Add(new Term{Morph=morph,Group=group,Weight=weight,Binary=binary});
        }
        public void Apply(VmdMotionSampler sampler,float frame)
        {
            currentSampler=sampler;currentFrame=frame;Apply(motionSample);
        }
        public void Apply(Func<string,float?> sample)
        {
            foreach(var binding in bindings)
            {
                if(binding.Renderer==null)continue;binding.Groups.Clear();
                foreach(var term in binding.Terms)
                {
                    float? sampled=sample(term.Morph);if(!sampled.HasValue)continue;float value=sampled.Value;
                    value=Mathf.Clamp01(value);if(term.Binary)value=value>=0.5f?1:0;
                    value*=term.Weight;float previous;binding.Groups.TryGetValue(term.Group,out previous);
                    binding.Groups[term.Group]=Mathf.Max(previous,value);
                }
                float total=0;foreach(float value in binding.Groups.Values)total+=value;
                binding.Renderer.SetBlendShapeWeight(binding.Index,Mathf.Clamp01(total)*100);
            }
        }
        public void Restore(){foreach(var binding in bindings)if(binding.Renderer!=null)binding.Renderer.SetBlendShapeWeight(binding.Index,binding.Original);}
    }
}
