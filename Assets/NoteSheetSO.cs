using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NoteSheet", menuName = "ScriptableObjects/NoteSheet", order = 1)]
public class NoteSheetSO : ScriptableObject
{
    [System.Serializable]
    public class NotePattern
    {
        public NoteEvent[] events = new NoteEvent[64];
        public NotePattern()
        {
            events = new NoteEvent[64];
        }
    }

    [System.Serializable]
    public class NoteEvent
    {
        public bool active;
        public Notes.NoteSignature note;
    }

    public int BPM;

    public NotePattern[] activeTicks;

}
