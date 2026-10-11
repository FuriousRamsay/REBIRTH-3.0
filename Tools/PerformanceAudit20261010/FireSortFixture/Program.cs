using System;using System.Linq;using System.Collections.Generic;
struct Vector3 {public float x,y,z;public float sqrMagnitude=>x*x+y*y+z*z;public static Vector3 operator -(Vector3 a,Vector3 b)=>new(){x=a.x-b.x,y=a.y-b.y,z=a.z-b.z};}
record struct Vector3i(int x,int y,int z){public Vector3 ToVector3Center()=>new(){x=x+.5f,y=y+.5f,z=z+.5f};}
class Before {
struct Candidate {
        public Vector3i Position;
        public float DistanceSquared;
    }
class CandidateComparer : IComparer<Candidate> {
        public static readonly CandidateComparer Instance = new CandidateComparer();

        public int Compare(Candidate x, Candidate y)
        {
            int distance = x.DistanceSquared.CompareTo(y.DistanceSquared);
            if (distance != 0)
                return distance;
            int xCompare = x.Position.x.CompareTo(y.Position.x);
            if (xCompare != 0)
                return xCompare;
            int yCompare = x.Position.y.CompareTo(y.Position.y);
            return yCompare != 0 ? yCompare : x.Position.z.CompareTo(y.Position.z);
        }
    }
static List<Candidate> FireCandidates=new(), NearFireCandidates=new(), MidFireCandidates=new(), FarFireCandidates=new(), SmokeCandidates=new();
static HashSet<Vector3i> Active=new(),DesiredFire=new(),DesiredSmoke=new(),DesiredLight=new(),DesiredSound=new();
const float NearBandDistance=50,MidBandDistance=100,FireRenderDistance=150;
public static string Run(Vector3i[] points,Vector3 playerPosition){Active=new(points);
        FireCandidates.Clear();
        NearFireCandidates.Clear();
        MidFireCandidates.Clear();
        FarFireCandidates.Clear();
        SmokeCandidates.Clear();
        DesiredFire.Clear();
        DesiredSmoke.Clear();
        DesiredLight.Clear();
        DesiredSound.Clear();

        float nearDistanceSquared = NearBandDistance * NearBandDistance;
        float midDistanceSquared = MidBandDistance * MidBandDistance;
        float fireDistanceSquared = FireRenderDistance * FireRenderDistance;

        foreach (Vector3i position in Active)
        {
            float distance = (position.ToVector3Center() - playerPosition).sqrMagnitude;
            if (distance > fireDistanceSquared)
                continue;

            Candidate candidate = new Candidate
            {
                Position = position,
                DistanceSquared = distance
            };
            FireCandidates.Add(candidate);
            if (distance <= nearDistanceSquared)
                NearFireCandidates.Add(candidate);
            else if (distance <= midDistanceSquared)
                MidFireCandidates.Add(candidate);
            else
                FarFireCandidates.Add(candidate);
        }

        FireCandidates.Sort(CandidateComparer.Instance);
        NearFireCandidates.Sort(CandidateComparer.Instance);
        MidFireCandidates.Sort(CandidateComparer.Instance);
        FarFireCandidates.Sort(CandidateComparer.Instance);

return string.Join("|",new[]{FireCandidates,NearFireCandidates,MidFireCandidates,FarFireCandidates}.Select(l=>string.Join(";",l.Select(c=>$"{c.Position.x},{c.Position.y},{c.Position.z}"))));}}
class After {
struct Candidate {
        public Vector3i Position;
        public float DistanceSquared;
    }
class CandidateComparer : IComparer<Candidate> {
        public static readonly CandidateComparer Instance = new CandidateComparer();

        public int Compare(Candidate x, Candidate y)
        {
            int distance = x.DistanceSquared.CompareTo(y.DistanceSquared);
            if (distance != 0)
                return distance;
            int xCompare = x.Position.x.CompareTo(y.Position.x);
            if (xCompare != 0)
                return xCompare;
            int yCompare = x.Position.y.CompareTo(y.Position.y);
            return yCompare != 0 ? yCompare : x.Position.z.CompareTo(y.Position.z);
        }
    }
static List<Candidate> FireCandidates=new(), NearFireCandidates=new(), MidFireCandidates=new(), FarFireCandidates=new(), SmokeCandidates=new();
static HashSet<Vector3i> Active=new(),DesiredFire=new(),DesiredSmoke=new(),DesiredLight=new(),DesiredSound=new();
const float NearBandDistance=50,MidBandDistance=100,FireRenderDistance=150;
public static string Run(Vector3i[] points,Vector3 playerPosition){Active=new(points);
        FireCandidates.Clear();
        NearFireCandidates.Clear();
        MidFireCandidates.Clear();
        FarFireCandidates.Clear();
        SmokeCandidates.Clear();
        DesiredFire.Clear();
        DesiredSmoke.Clear();
        DesiredLight.Clear();
        DesiredSound.Clear();

        float nearDistanceSquared = NearBandDistance * NearBandDistance;
        float midDistanceSquared = MidBandDistance * MidBandDistance;
        float fireDistanceSquared = FireRenderDistance * FireRenderDistance;

        foreach (Vector3i position in Active)
        {
            float distance = (position.ToVector3Center() - playerPosition).sqrMagnitude;
            if (distance > fireDistanceSquared)
                continue;

            Candidate candidate = new Candidate
            {
                Position = position,
                DistanceSquared = distance
            };
            FireCandidates.Add(candidate);
        }

        FireCandidates.Sort(CandidateComparer.Instance);
        // Each band is a subsequence of the same total ordering. Partition after sorting
        // to preserve all distance/position tie-breaks without sorting every candidate twice.
        for (int i = 0; i < FireCandidates.Count; i++)
        {
            Candidate candidate = FireCandidates[i];
            if (candidate.DistanceSquared <= nearDistanceSquared)
                NearFireCandidates.Add(candidate);
            else if (candidate.DistanceSquared <= midDistanceSquared)
                MidFireCandidates.Add(candidate);
            else
                FarFireCandidates.Add(candidate);
        }

return string.Join("|",new[]{FireCandidates,NearFireCandidates,MidFireCandidates,FarFireCandidates}.Select(l=>string.Join(";",l.Select(c=>$"{c.Position.x},{c.Position.y},{c.Position.z}"))));}}

class Program{static void Main(){var random=new Random(1729);for(int run=0;run<1000;run++){var points=Enumerable.Range(0,run%257).Select(i=>new Vector3i(random.Next(-155,156),random.Next(-5,6),random.Next(-155,156))).Concat(new[]{new Vector3i(50,0,0),new Vector3i(100,0,0),new Vector3i(150,0,0),new Vector3i(-50,0,0)}).ToArray();var player=new Vector3{x=.5f,y=.5f,z=.5f};if(Before.Run(points,player)!=After.Run(points,player))throw new Exception("Order differs: "+run);}Console.WriteLine("PASS: 1000 seeded populations plus exact 50/100/150m boundaries retain identical global and per-band order.");Console.WriteLine("Actual production comparator and candidate build loops extracted; vector/list environment doubles. Not native rendering/FPS evidence.");}}
