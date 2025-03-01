using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Classic
{
    public class ClassicRobotInfo : MonoBehaviour
    {
        public int m_robot_type;
        public RobotInfo m_robot_info;
    }

    public static class GetClassic
    {
        public static ClassicData ClassicData;
        public static Pig pig;
        static byte[] pal;
        static Color32[] pal32;
        static uint[] palu;
        static Shader shader;
        public static List<GameObject> ClassicRobots;
        static Dictionary<int, Material> ClassicMats = new Dictionary<int, Material>();
        static Dictionary<int, AudioClip> ClassicClips = new Dictionary<int, AudioClip>();

        static void MyInit()
        {
            if (ClassicData != null)
                return;
            string dir = "c:\\games\\d1x-rebirth\\data";
            //var hogFile = Path.Combine(dir, "descent.hog");
            var hogFile = @"c:\games\descent2\descent2.hog";
            Hog hog = new Hog(hogFile);
            byte[] vgapal = hog.ItemData("groupa.256");
            pal = ClassicLoader.VgaPalConv(vgapal);

            pal32 = new Color32[256];
            for (int i = 0; i < 256; i++)
                pal32[i] = new Color32(pal[i * 3], pal[i * 3 + 1], pal[i * 3 + 2], 255);

            palu = new uint[256];
            for (int i = 0; i < 256; i++)
                palu[i] = ((uint)pal[i * 3] << 0) | ((uint)pal[i * 3 + 1] << 8) | ((uint)pal[i * 3 + 2] << 16) | (255u << 24);

            var pigFile = Path.Combine(dir, "descent.pig");
            pigFile = @"c:\\games\\descent2\\groupa.pig";

            pig = new Pig(pigFile);

            pig.ReadD2Sound(@"c:\\games\descent2\\descent2.s11");

            pig.ReadTableData(out ClassicData);

            shader = Shader.Find("Standard");
            //shader = Shader.Find("Diffuse");
        }

        enum PolyOp
        {
            EOF = 0,       //eof
            DEFPOINTS = 1,       //defpoints
            FLATPOLY = 2,       //flat-shaded polygon
            TMAPPOLY = 3,       //texture-mapped polygon
            SORTNORM = 4,       //sort by normal
            RODBM = 5,       //rod bitmap
            SUBCALL = 6,       //call a subobject
            DEFP_START = 7,       //defpoints with start
            GLOW = 8       //glow value for next poly
        }

        static readonly vms_vector[] points = new vms_vector[1000];
        static vms_vector pointPos = default(vms_vector);

        struct UPoint
        {
            //Fix nx, ny, nz, x, y, z, u, v;
            public vms_vector norm;
            public vms_vector p;
            public g3s_uvl uvl;
        };

        static Dictionary<UPoint, int> uPointIdx = new Dictionary<UPoint, int>();
        static List<UPoint> uPoints = new List<UPoint>();
        static List<int>[] tris;

        static void AddPoint(int bitmap, ref vms_vector norm, ref vms_vector p, ref g3s_uvl uvl)
        {
            var uPoint = new UPoint { norm = norm, p = p, uvl = uvl };
            int idx;
            if (!uPointIdx.TryGetValue(uPoint, out idx))
            {
                uPointIdx[uPoint] = idx = uPoints.Count;
                uPoints.Add(uPoint);
            }
            tris[bitmap].Add(idx);
        }

        static void DumpModelData(BinaryReader r)
        {
            //Debug.Log("Sub start " + r.BaseStream.Position);
            for (PolyOp op; (op = (PolyOp)r.ReadInt16()) != PolyOp.EOF;)
            {
                int n, s, i, color, bitmap, i1, i2;
                ushort[] pointIdx;
                g3s_uvl[] uvls;
                vms_vector v = default(vms_vector), norm = default(vms_vector);
                //vms_vector[] pvs;
                vms_vector oldPointPos;
                g3s_uvl uvl = default(g3s_uvl);
                var startPos = r.BaseStream.Position - 2;
                //Debug.Log(startPos + ": " + op + " ");
                switch (op)
                {
                    case PolyOp.DEFPOINTS:
                        n = r.ReadInt16();
                        //pvs = new vms_vector[n];
                        //pvs.Read(r);
                        for (i = 0; i < n; i++)
                        {
                            points[i].Read(r);
                            points[i] += pointPos;
                        }
                        //Debug.Log(n + " " + string.Join(", ", pvs.Select(x => x.ToString()).ToArray()));
                        break;
                    case PolyOp.DEFP_START:
                        n = r.ReadInt16();
                        s = r.ReadInt16();
                        r.ReadInt16(); // align
                                       //pvs = new vms_vector[n];
                                       //pvs.Read(r);
                        for (i = 0; i < n; i++)
                        {
                            points[i + s].Read(r);
                            points[i + s] += pointPos;
                        }
                        //Debug.Log(s + " " + n + " " + string.Join(", ", pvs.Select(x => x.ToString()).ToArray()));
                        break;
                    case PolyOp.FLATPOLY:
                        n = r.ReadInt16();
                        v.Read(r);
                        norm.Read(r);
                        color = r.ReadInt16();
                        pointIdx = new ushort[n];
                        pointIdx.Read(r);
                        if ((n & 1) == 0)
                            r.ReadInt16(); // align
                        //Debug.Log(v + " " + norm + " c=" + color + " " + n + " " + string.Join(", ", pointIdx.Select(x => x.ToString()).ToArray()));
                        for (i = 2; i < n; i++)
                        {
                            AddPoint(0, ref norm, ref points[pointIdx[0]], ref uvl);
                            AddPoint(0, ref norm, ref points[pointIdx[i - 1]], ref uvl);
                            AddPoint(0, ref norm, ref points[pointIdx[i]], ref uvl);
                        }
                        break;
                    case PolyOp.TMAPPOLY:
                        n = r.ReadInt16();
                        v.Read(r);
                        norm.Read(r);
                        bitmap = r.ReadInt16();
                        pointIdx = new ushort[n];
                        pointIdx.Read(r);
                        if ((n & 1) == 0)
                            r.ReadInt16(); // align
                        uvls = new g3s_uvl[n];
                        uvls.Read(r);
                        //Debug.Log(v + " " + norm + " bm=" + bitmap + " " + n + " " + string.Join(", ", pointIdx.Select(x => x.ToString()).ToArray()) + " " + string.Join(", ", uvls.Select(x => x.ToString()).ToArray()));
                        for (i = 2; i < n; i++)
                        {
                            AddPoint(bitmap, ref norm, ref points[pointIdx[0]], ref uvls[0]);
                            AddPoint(bitmap, ref norm, ref points[pointIdx[i - 1]], ref uvls[i - 1]);
                            AddPoint(bitmap, ref norm, ref points[pointIdx[i]], ref uvls[i]);
                        }
                        break;
                    case PolyOp.SORTNORM:
                        r.ReadInt16(); // align
                        v.Read(r);
                        norm.Read(r);
                        i1 = r.ReadInt16();
                        i2 = r.ReadInt16();
                        //Debug.Log(v + " " + norm + " " + i1 + " " + i2);
                        var pos = r.BaseStream.Position;
                        r.BaseStream.Position = startPos + i1;
                        DumpModelData(r);
                        r.BaseStream.Position = startPos + i2;
                        DumpModelData(r);
                        r.BaseStream.Position = pos;
                        break;
                    case PolyOp.RODBM:
                        bitmap = r.ReadInt16(); // align
                        v.Read(r);
                        i1 = r.ReadInt16();
                        r.ReadInt16(); // align
                        norm.Read(r);
                        i2 = r.ReadInt16();
                        r.ReadInt16(); // align
                        //Debug.Log(v + " " + norm + " " + i1 + " " + i2);
                        break;
                    case PolyOp.SUBCALL:
                        i2 = r.ReadInt16();
                        v.Read(r);
                        i1 = r.ReadInt16();
                        r.ReadInt16(); // align
                        //Debug.Log("anim=" + i2 + " " + v + " " + i1);
                        pos = r.BaseStream.Position;
                        r.BaseStream.Position = startPos + i1;
                        oldPointPos = pointPos;
                        pointPos += v;
                        DumpModelData(r);
                        pointPos = oldPointPos;
                        r.BaseStream.Position = pos;
                        break;
                    case PolyOp.GLOW:
                        i1 = r.ReadInt16();
                        //Debug.Log(i1.ToString());
                        break;
                    default:
                        throw new Exception();
                }
            }
            //Debug.Log("Sub done");
        }

        static unsafe Texture2D MkTex(int bmidx)
        {
            int width, height;
            byte[] img = pig.GetBitmap(bmidx - 1, out width, out height);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tex.filterMode = FilterMode.Point;
            var pix = new Color32[width * height];
            for (int ofs = 0, ofsend = width * (height - 1); ofs <= ofsend; ofs += width)
                for (var x = 0; x < width; x++)
                    pix[ofs + x] = pal32[img[(0 + ofs) + x]];
            //var pixbuf = new byte[width * height * 4];
            //Buffer.BlockCopy(pix, 0, pixbuf, 0, width * height * 4);
            fixed (Color32* p = pix)
                tex.LoadRawTextureData((IntPtr)p, width * height * 4);
            //tex.LoadRawTextureData(pixbuf);
            //tex.SetPixels32(pix);
            tex.Apply();
            return tex;
        }

        static Material MkMat(int bmidx)
        {
            Material mat;
            if (ClassicMats.TryGetValue(bmidx, out mat))
                return mat;
            mat = new Material(shader);
            var tex = MkTex(bmidx);
            mat.SetTexture("_MainTex", tex);
            //mat.mainTexture = MkTex(bmidx);
            mat.SetTexture("_EmissionMap", tex);
            //mat.SetColor("_Color", Color.white);
            mat.SetColor("_EmissionColor", Color.white * .25f);
            mat.EnableKeyword("_EMISSION");
            ClassicMats.Add(bmidx, mat);
            return mat;
        }

        public static bool ClassicInit()
        {
        	MyInit();
        	return ClassicData != null;
        }

        static void CreateRobotModels()
        {
            if (ClassicRobots != null)
                return;
            MyInit();
            ClassicRobots = new List<GameObject>();
            for (int ri = 0; ri < ClassicData.N_robot_types; ri++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                //int n = gameObject.name[gameObject.name.Length - 2] - '0';
                //Debug.Log(n);
                PolyModel model = ClassicData.PolygonModels[ClassicData.RobotInfo[ri].model_num];

                var r = new BinaryReader(new MemoryStream(model.data));
                tris = new List<int>[model.n_textures];
                for (int i = 0; i < model.n_textures; i++)
                    tris[i] = new List<int>();
                DumpModelData(r);

                var mesh = new Mesh();
                mesh.vertices = uPoints.Select(x => x.p.ToVector3()).ToArray();
                mesh.normals = uPoints.Select(x => x.norm.ToVector3()).ToArray();
                mesh.uv = uPoints.Select(x => new Vector2(x.uvl.u.ToFloat(), x.uvl.v.ToFloat())).ToArray();
                mesh.subMeshCount = model.n_textures;
                for (int i = 0; i < model.n_textures; i++)
                    mesh.SetTriangles(tris[i].ToArray(), i);
                var filt = go.GetComponent<MeshFilter>();
                filt.mesh = mesh;

                var rend = go.GetComponent<MeshRenderer>();
                //Debug.Log("rend");
                //Debug.Log(rend);
                var mats = new Material[model.n_textures];
                for (int i = 0; i < model.n_textures; i++)
                    mats[i] = MkMat(ClassicData.ObjBitmaps[ClassicData.ObjBitmapPtrs[model.first_texture + i]].index);
                rend.materials = mats;
                var cri = go.AddComponent<ClassicRobotInfo>();
                cri.m_robot_type = ri;
                cri.m_robot_info = ClassicData.RobotInfo[ri];
                UnityEngine.Object.DontDestroyOnLoad(go);
                ClassicRobots.Add(go);
            }
            Debug.Log("createmodels robot0 see=" + ClassicData.RobotInfo[0].see_sound);
            Debug.Log("createmodels robot0 component see=" + ClassicRobots[0].GetComponent<ClassicRobotInfo>().m_robot_info.see_sound);
        }

        public static int NumRobots { get { CreateRobotModels(); return ClassicData.N_robot_types; } }

        public static GameObject InstantiateRobot(int n)
        {
            CreateRobotModels();
            return UnityEngine.Object.Instantiate<GameObject>(ClassicRobots[n]);
        }

        public static bool IsClassicBot(Robot robot, out int botNum)
        {
            botNum = -1;
            return robot.gameObject.name.StartsWith("entity_enemy_classic_") && int.TryParse(robot.gameObject.name.Substring(21), out botNum);
        }

        public static AudioClip GetClip(int sndnum)
        {
            int sndidx;
            if (ClassicData == null || sndnum < 0 || sndnum >= ClassicData.Sounds.Length || (sndidx = ClassicData.Sounds[sndnum]) == 255)
                return AudioClip.Create("", 0, 1, 44100, false);
            AudioClip clip;
            if (ClassicClips.TryGetValue(sndidx, out clip))
                return clip;
            var sound = pig.sounds[sndidx];
            int p = pig.soundDataOfs + sound.offset, len = sound.length;
            byte[] buf = pig.soundData;

            clip = AudioClip.Create("classic_" + sndidx, len, 1, 11025, false);
            float[] fdata = new float[len];
            for (int i = 0; i < len; i++)
                fdata[i] = (buf[p + i] - 128) / 128f;
            clip.SetData(fdata, 0);
            ClassicClips.Add(sndidx, clip);
            return clip;
        }

/*
        public static int PlaySound(int sndnum, Vector3 pos3d, float vol, float pitch, float offset, UnityAudio.SoundType st, bool loop = false, float amt_3d = 1f)
        {
            var audio = Overload.GameManager.m_audio;
            int audioSlot = audio.FindNextOpenAudioSlot();
            if (audioSlot < 0)
            {
                if (audio.m_debug_sounds)
                {
                    Debug.Log("NO FREE SOUND SLOTS!");
                }
                return -1;
            }
            audio.m_a_object[audioSlot].transform.parent = null;
            audio.m_a_object[audioSlot].transform.localPosition = pos3d;
            var asrc = audio.m_a_source[audioSlot];
            asrc.enabled = true;
            asrc.clip = GetClip(sndnum);
            asrc.time = Mathf.Clamp(offset, 0f, asrc.clip.length);
            asrc.volume = vol;
            asrc.spatialBlend = ((st != 0) ? 0f : amt_3d);
            asrc.loop = loop;
            asrc.maxDistance = ((!Overload.GameplayManager.IsMultiplayer) ? 250f : 70f);
            audio.m_a_original_pitch[audioSlot] = 1f + pitch;
            asrc.pitch = audio.m_a_original_pitch[audioSlot];
            asrc.outputAudioMixerGroup = audio.m_mixer_groups[(int)audio.m_default_audio_group];
            asrc.bypassReverbZones = (st == UnityAudio.SoundType.ST_2D);
            asrc.reverbZoneMix = ((st != UnityAudio.SoundType.ST_2D) ? 1f : UnityEngine.Random.Range(0f, 0.002f));
            audio.m_a_object[audioSlot].SetActive(true);
            asrc.Play();
            return audioSlot;
        }
*/

    }
}
