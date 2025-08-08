using System.Collections.Generic;
using UnityEngine;

namespace AI
{
    [System.Serializable]
    public class RoleProfile
    {
        public string roleName;
        public List<string> questions;
    }

    public class RoleProfileLoader : MonoBehaviour
    {
        private const string roleProfilesPath = "RoleProfiles/";

        public RoleProfile LoadRoleProfile(string role)
        {
            TextAsset json = Resources.Load<TextAsset>(roleProfilesPath + role);
            if (json != null)
            {
                return JsonUtility.FromJson<RoleProfile>(json.text);
            }
            else
            {
                Debug.LogError("Role profile not found: " + role);
                return null;
            }
        }
    }
}