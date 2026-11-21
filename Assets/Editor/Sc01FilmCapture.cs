using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public static class Sc01FilmCapture
{
    private const string ScenePath = BuildAndroid.ScenePath;
    private const string TimelinePath = "Assets/Scenes/SC01_UnopenedLetter.playable";
    private const int Width = 1920, Height = 1080, Fps = 30, FrameCount = 450;
    private static EditorApplication.CallbackFunction job;

    [MenuItem("ShotField/SC01/Render Unopened Letter")]
    public static void Start()
    {
        if (job != null || EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open SC01_Main in Edit Mode with no other capture running.");
        var actorA = GameObject.Find("SC01_Performer_A").GetComponentInChildren<Animator>();
        var actorB = GameObject.Find("SC01_Performer_B").GetComponentInChildren<Animator>();
        var letter = GameObject.Find("Envelope").transform;
        var source = GameObject.Find("Director").GetComponent<Camera>();
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        if (!timeline)
        {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);
            timeline.editorSettings.frameRate = Fps;
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = 15;
            var idle = actorA.runtimeAnimatorController.animationClips.Single(c => c.name == "Idle");
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Scenes/SC01_PerformerWalk.anim");
            var trackA = timeline.CreateTrack<AnimationTrack>(null, "A | Enter and offer letter");
            var trackB = timeline.CreateTrack<AnimationTrack>(null, "B | Receive and return letter");
            Clip(trackA, walk, 0, 3.2);
            Clip(trackA, idle, 3.2, 11.8);
            Clip(trackB, idle, 0, 15);
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
        }
        var tracks = timeline.GetOutputTracks().OfType<AnimationTrack>().ToArray();
        if (tracks.Length != 2 || Math.Abs(timeline.duration - 15) > .01)
            throw new InvalidOperationException("Unexpected SC01 Timeline.");
        var folder = Path.GetFullPath("Temp/SC01_Film_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(folder);
        var saved = new[] {actorA.transform, actorB.transform, letter}.SelectMany(t => t.GetComponentsInChildren<Transform>(true)).Distinct()
            .Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToArray();
        var holder = new GameObject("SC01 Film Capture") {hideFlags = HideFlags.HideAndDontSave};
        var director = holder.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        director.playableAsset = timeline;
        director.SetGenericBinding(tracks[0], actorA);
        director.SetGenericBinding(tracks[1], actorB);
        director.RebuildGraph();
        var cam = holder.AddComponent<Camera>();
        cam.CopyFrom(source);
        cam.enabled = false;
        cam.cullingMask &= ~(1 << 5);
        cam.aspect = (float)Width / Height;
        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        var frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        var ambientMode = RenderSettings.ambientMode;
        var ambient = RenderSettings.ambientLight;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.74f, .68f, .59f);
        var lightObject = new GameObject("SC01 Film Bounce") {hideFlags = HideFlags.HideAndDontSave};
        lightObject.transform.position = new Vector3(.4f, 2.62f, 2.35f);
        var fill = lightObject.AddComponent<Light>();
        fill.type = LightType.Point; fill.intensity = 2.4f; fill.range = 5; fill.color = new Color(1,.88f,.76f);
        var tableLetter = letter.position;
        var letterRotation = letter.rotation;
        int index = 0;
        job = () =>
        {
            try
            {
                for (int batch = 0; batch < 3 && index < FrameCount; batch++, index++)
                {
                    float time = index / (float)Fps;
                    director.time = time;
                    director.Evaluate();
                    Stage(time, actorA, actorB, letter, tableLetter, letterRotation, cam);
                    var prior = RenderTexture.active;
                    try
                    {
                        cam.targetTexture = target;
                        RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest {destination = target});
                        RenderTexture.active = target;
                        frame.ReadPixels(new Rect(0,0,Width,Height),0,0);
                        File.WriteAllBytes(Path.Combine(folder,$"{index:D4}.jpg"),frame.EncodeToJPG(94));
                    }
                    finally { RenderTexture.active = prior; cam.targetTexture = null; }
                }
                if (index < FrameCount) return;
                File.WriteAllText(Path.Combine(folder,"complete.txt"),"timeline=15.0; capture=0..449; fps=30; frames=450; shots=0,120,300");
                Debug.Log("SC01 capture complete: "+folder);
                Finish();
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(folder,"failure.txt"),e.ToString()); Debug.LogException(e); Finish(); }
        };
        void Finish()
        {
            EditorApplication.update -= job;
            job = null;
            director.Stop();
            foreach (var (t,p,q,z) in saved) if (t) {t.localPosition=p;t.localRotation=q;t.localScale=z;}
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;
            UnityEngine.Object.DestroyImmediate(holder);
            UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(frame);
        }
        EditorApplication.update += job;
    }

    private static void Clip(AnimationTrack track, AnimationClip clip, double start, double duration)
    {
        var segment = track.CreateClip(clip);
        segment.start=start;segment.duration=duration;
        ((AnimationPlayableAsset)segment.asset).loop=AnimationPlayableAsset.LoopMode.On;
    }

    private static float Ease(float t,float from,float to) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,t));

    private static void Stage(float t,Animator a,Animator b,Transform letter,Vector3 desk,Quaternion flat,Camera cam)
    {
        float travel=Mathf.Clamp01(t/3.2f);
        Vector3 aStart=new Vector3(-1.85f,0,.85f), aEnd=new Vector3(.10f,0,1.81f);
        a.transform.SetPositionAndRotation(Vector3.Lerp(aStart,aEnd,travel),Quaternion.LookRotation(new Vector3(.85f,0,.65f)));
        b.transform.SetPositionAndRotation(new Vector3(1.18f,0,3.05f),Quaternion.Euler(0,198,0));
        var place=Ease(t,4.4f,6.15f);
        var take=Ease(t,7.1f,8.8f);
        var putBack=Ease(t,11.7f,13.45f);
        Vector3 aHold=new Vector3(.52f,1.03f,2.05f), bHold=new Vector3(.98f,1.08f,2.76f);
        var aHand=Vector3.Lerp(aHold,desk+Vector3.up*.055f,place);
        var bHand=Vector3.Lerp(desk+Vector3.up*.055f,bHold,take*(1-putBack));
        AimArm(a,aHand,1-Ease(t,6.15f,7f)); 
        AimArm(b,bHand,Ease(t,6.4f,7.1f)*(1-Ease(t,13.45f,14.3f)), true);
        var carry=Vector3.Lerp(aHold-Vector3.up*.055f,desk,place);
        if(t<4.4f)carry+=a.transform.position-aEnd;
        var handA=a.GetBoneTransform(HumanBodyBones.RightHand);
        var handB=b.GetBoneTransform(HumanBodyBones.LeftHand);
        if(t<6.15f && handA)carry=Vector3.Lerp(handA.position-Vector3.up*.035f,desk,Ease(t,5.9f,6.15f));
        if(t>=7.1f && t<13.45f && handB)
        {
            var grip=Ease(t,7.1f,7.35f)*(1-Ease(t,13.15f,13.45f));
            carry=Vector3.Lerp(desk,handB.position-Vector3.up*.03f,grip);
        }
        var inspect=Ease(t,9.15f,10.6f)*(1-putBack);
        letter.SetPositionAndRotation(carry,Quaternion.AngleAxis(inspect*70,Vector3.right)*flat);
        var head=b.GetBoneTransform(HumanBodyBones.Head);
        if(head)head.rotation=Quaternion.AngleAxis(14*Ease(t,.6f,2f)-26*Ease(t,6.3f,7.5f)+24*Ease(t,13.3f,14.8f),b.transform.right)*head.rotation;
        Vector3 pos,look;
        if(t<4){pos=Vector3.Lerp(new Vector3(-2.10f,1.60f,1.14f),new Vector3(-1.86f,1.53f,1.34f),Ease(t,0,4));look=new Vector3(.53f,1.03f,2.38f);cam.fieldOfView=57;}
        else if(t<10){pos=Vector3.Lerp(new Vector3(-.60f,1.42f,2.98f),new Vector3(-.40f,1.32f,2.70f),Ease(t,4,10));look=Vector3.Lerp(desk+Vector3.up*.20f,bHold,Ease(t,7.2f,9.2f));cam.fieldOfView=48;}
        else {pos=Vector3.Lerp(new Vector3(-.43f,1.51f,2.05f),new Vector3(-.32f,1.54f,2.28f),Ease(t,10,15));look=Vector3.Lerp(bHold,new Vector3(1.18f,1.48f,3.05f),Ease(t,12.6f,14.7f));cam.fieldOfView=43;}
        cam.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(look-pos));
    }

    private static void AimArm(Animator animator,Vector3 target,float weight,bool left=false)
    {
        var upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
        var lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
        var hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
        if(!upper||!lower||!hand||weight<=0)return;
        target=Vector3.Lerp(hand.position,target,weight);
        Vector3 origin=upper.position, d=target-origin;
        float a=Vector3.Distance(origin,lower.position), b=Vector3.Distance(lower.position,hand.position);
        if(a<.001f||b<.001f||d.sqrMagnitude<.0001f)return;
        float length=Mathf.Clamp(d.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
        Vector3 axis=d.normalized;
        float along=(a*a-b*b+length*length)/(2*length);
        var bend=Vector3.ProjectOnPlane(-Vector3.up,axis).normalized;
        Vector3 elbow=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,origin+axis*length-lower.position)*lower.rotation;
    }
}
