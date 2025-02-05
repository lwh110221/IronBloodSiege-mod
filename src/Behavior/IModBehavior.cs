using System;

namespace IronBloodSiege.Behavior
{
    public interface IModBehavior
    {
        void OnModEnabled();
        void OnModDisabled();
        void OnSceneChanged();
    }
} 