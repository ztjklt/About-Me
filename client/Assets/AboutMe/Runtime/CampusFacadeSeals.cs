using System.Collections.Generic;
using UnityEngine;

namespace AboutMe
{
    /// <summary>
    /// Flush, visible closures for the upper classroom shells. The source campus
    /// leaves a walkable gap between the wing decks and the recessed window bays.
    /// Keep these boxes shared by rendering, PhysX, and the authoritative geometry.
    /// </summary>
    public static class CampusFacadeSeals
    {
        static CampusSolid[] solids;
        public static CampusSolid[] Solids=>solids??(solids=Create());

        static CampusSolid[] Create()
        {
            var result=new List<CampusSolid>();
            void Add(string name,Vector3 center,Vector3 size)
            {result.Add(new CampusSolid{name=name,center=center,size=size});}

            // Slabs overlap both the upper deck and the classroom floors. Their
            // top is 4.60 m, below the existing classroom floor's 4.66 m top.
            foreach(int side in new[]{-1,1})
            {
                Add("upper_facade_wing_apron",new Vector3(side*30.65f,4.30f,2),new Vector3(1.90f,.60f,40.20f));
                Add("upper_facade_corner_apron",new Vector3(side*30.65f,4.30f,23.6f),new Vector3(1.90f,.60f,3.10f));
                // A visible lower wall meets every existing window sill and
                // blocks entry into the non-playable classroom recess.
                Add("upper_facade_wing_sill",new Vector3(side*31.45f,5.05f,2),new Vector3(.90f,1.10f,40.20f));
                foreach(float z in new[]{-18f,-10f,-2f,6f,14f,22f})
                    Add("upper_facade_wing_cheek",new Vector3(side*33.125f,6.65f,z),new Vector3(2.90f,4.30f,.80f));
            }
            foreach(int side in new[]{-1,1})
            {
                Add("upper_facade_rear_apron",new Vector3(side*20,4.30f,24.55f),new Vector3(24.20f,.60f,1.10f));
                Add("upper_facade_rear_sill",new Vector3(side*20,5.05f,24.90f),new Vector3(24.20f,1.10f,.80f));
                foreach(float x in new[]{8f,16f,24f,32f})
                    Add("upper_facade_rear_cheek",new Vector3(side*x,6.65f,26.325f),new Vector3(.80f,4.30f,2.90f));
            }
            // The central entrance divides the classroom bays, but its upper
            // facade still needs the same continuous floor connection.
            Add("upper_facade_rear_center_apron",new Vector3(0,4.30f,24.55f),new Vector3(16.20f,.60f,1.10f));
            return result.ToArray();
        }
    }
}
