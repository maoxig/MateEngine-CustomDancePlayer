using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    public static class VmdSupportRetargeting
    {
        // Closest pelvis translation satisfying both leg reach balls. No
        // previous-frame state: scrubbing and playback produce the same pose.
        public static Vector3 ClosestReachableRoot(Vector3 root, Vector3 a, float ra, Vector3 b, float rb)
        {
            Vector3 pa = Project(root, a, ra);
            if (Inside(pa,b,rb)) return pa;
            Vector3 pb = Project(root, b, rb);
            if (Inside(pb,a,ra)) return pb;
            Vector3 delta = b - a;
            float distance = delta.magnitude;
            if (distance < 0.000001f) return Project(root, a, Mathf.Min(ra, rb));
            Vector3 axis = delta / distance;
            if (distance >= ra + rb) return a + axis * (distance * ra / (ra + rb));
            float along = (ra * ra - rb * rb + distance * distance) / (2f * distance);
            Vector3 center = a + axis * along;
            float radius = Mathf.Sqrt(Mathf.Max(0f, ra * ra - along * along));
            Vector3 radial = root - center;
            radial -= axis * Vector3.Dot(radial, axis);
            if (radial.sqrMagnitude < 0.00000001f)
            {
                radial = Vector3.up - axis * Vector3.Dot(Vector3.up, axis);
                if (radial.sqrMagnitude < 0.00000001f) radial = Vector3.right;
            }
            return center + radial.normalized * radius;
        }

        public static bool TryKeepHorizontalRoot(Vector3 root,Vector3 a,float ra,Vector3 b,float rb,bool both,out Vector3 result)
        {
            result=root;
            float minA,maxA,minB,maxB;
            if(!HeightInterval(root,a,ra,out minA,out maxA))return false;
            float low=minA,high=maxA;
            if(both) {
                if(!HeightInterval(root,b,rb,out minB,out maxB))return false;
                low=Mathf.Max(low,minB);high=Mathf.Min(high,maxB);
            }
            if(low>high)return false;
            result.y=Mathf.Clamp(root.y,low,high);return true;
        }
        private static bool HeightInterval(Vector3 root,Vector3 center,float radius,out float low,out float high)
        {
            float x=root.x-center.x,z=root.z-center.z;
            float vertical=radius*radius-x*x-z*z;low=high=0f;
            if(vertical<0f)return false;
            float reach=Mathf.Sqrt(vertical);low=center.y-reach;high=center.y+reach;return true;
        }

        private static bool Inside(Vector3 point,Vector3 center,float radius)
        {
            // A projected float vector can lie a few ulps beyond the sphere.
            // Without a tolerance that valid projection incorrectly falls into
            // the intersection-circle branch, potentially far from the root.
            float squared=radius*radius;
            return (point-center).sqrMagnitude<=squared+Mathf.Max(0.0000001f,squared*0.00001f);
        }

        private static Vector3 Project(Vector3 point, Vector3 center, float radius)
        {
            Vector3 delta = point - center;
            float length = delta.magnitude;
            return length <= radius ? point : center + delta * (radius / length);
        }
    }
}
