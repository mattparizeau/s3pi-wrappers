﻿using System;
using System.Collections.Generic;
using System.Text;
using s3pi.Interfaces;
using System.IO;
using s3pi.Custom;
namespace s3piwrappers
{
    public enum ClipEventType
    {
        Parent = 0x0001,
        UnParent = 0x0002,
        Sound = 0x0003,
        Script = 0x0004,
        Effect = 0x0005,
        Visibility = 0x0006,
        DestroyProp = 0x0009,
        StopEffect = 0x000A

    }
    public abstract class DependentElement : AHandlerElement
    {
        protected DependentElement(int apiVersion, EventHandler handler)
            : base(apiVersion, handler)
        {

        }
        protected DependentElement(int apiVersion, EventHandler handler, Stream s)
            : base(apiVersion, handler)
        {
            Parse(s);
        }
        protected DependentElement(int apiVersion, EventHandler handler, DependentElement basis)
            : base(apiVersion, handler)
        {
            MemoryStream ms = new MemoryStream();
            basis.UnParse(ms);
            ms.Position = 0L;
            Parse(ms);
        }
        protected abstract void Parse(Stream s);
        public abstract void UnParse(Stream s);



        public override AHandlerElement Clone(EventHandler handler)
        {
            MemoryStream ms = new MemoryStream();
            UnParse(ms);
            return (AHandlerElement)Activator.CreateInstance(GetType(), new object[] { 0, handler, ms });
        }

        public override List<string> ContentFields
        {
            get { return GetContentFields(0, GetType()); }
        }

        public override int RecommendedApiVersion
        {
            get { return 1; }
        }
    }

    public class DependentElementList<T> : AResource.DependentList<T>
        where T : DependentElement, IEquatable<T>
    {
        public DependentElementList(EventHandler handler) : base(handler) { }
        public DependentElementList(EventHandler handler, Stream s) : base(handler, s) { }
        public override void Add()
        {
            base.Add(new object[] { });
        }

        protected override T CreateElement(Stream s)
        {
            return (T)Activator.CreateInstance(typeof(T), new object[] { 0, elementHandler, s });
        }

        protected override void WriteElement(Stream s, T element)
        {
            element.UnParse(s);
        }
    }
    /// <summary>
    /// Wrapper for the animation resource
    /// </summary>
    public class ClipResource : AResource
    {

        public class CountedOffsetItemList<T> : DependentElementList<T>
            where T : DependentElement, IEquatable<T>
        {
            public CountedOffsetItemList(EventHandler handler) : base(handler) { }
            public CountedOffsetItemList(EventHandler handler, Stream s) : base(handler, s) { }
            protected override void Parse(Stream s)
            {
                base.Clear();
                BinaryReader br = new BinaryReader(s);
                uint count = ReadCount(s);
                long startOffset = s.Position;
                long[] offsets = new long[count];
                for (int i = 0; i < count; i++)
                {
                    offsets[i] = br.ReadUInt32() + startOffset;
                }
                long endOffset = s.Position;
                for (int i = 0; i < count; i++)
                {
                    s.Seek(offsets[i], SeekOrigin.Begin);
                    ((IList<T>)this).Add(CreateElement(s));
                }
            }
            public override void UnParse(Stream s)
            {
                BinaryWriter bw = new BinaryWriter(s);
                uint[] offsets = new uint[base.Count];
                WriteCount(s, (uint)base.Count);
                long startOffset = s.Position;
                for (int i = 0; i < base.Count; i++) { bw.Write(offsets[i]); }
                for (int i = 0; i < base.Count; i++)
                {
                    offsets[i] = (uint)(s.Position - startOffset);
                    WriteElement(s, this[i]);
                }
                long endOffset = s.Position;
                s.Seek(startOffset, SeekOrigin.Begin);
                for (int i = 0; i < base.Count; i++) { bw.Write(offsets[i]); }
                s.Seek(endOffset, SeekOrigin.Begin);
            }
        }
        #region ActorSlot

        public class ActorSlotTable : DependentElement
        {
            private CountedOffsetItemList<ActorSlotTableEntry> mEntries;
            public ActorSlotTable(int apiVersion, EventHandler handler) : base(apiVersion, handler) { }
            public ActorSlotTable(int apiVersion, EventHandler handler, Stream s) : base(apiVersion, handler, s) { }

            public CountedOffsetItemList<ActorSlotTableEntry> Entries
            {
                get { return mEntries; }
                set { mEntries = value; OnElementChanged(); }
            }

            public string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < mEntries.Count; i++)
                    {
                        sb.AppendFormat("==[{0}]==\n{1}\n", i, mEntries[i].Value);
                    }
                    return sb.ToString();
                }
            }
            protected override void Parse(Stream s)
            {
                BinaryReader br = new BinaryReader(s);
                mEntries = new CountedOffsetItemList<ActorSlotTableEntry>(handler, s);
            }
            public override void UnParse(Stream s)
            {
                BinaryWriter bw = new BinaryWriter(s);
                mEntries.UnParse(s);
            }
        }
        public class ActorSlotTableEntry : DependentElement, IEquatable<ActorSlotTableEntry>
        {
            private CountedOffsetItemList<ActorSlotEntry> mEntries;
            public ActorSlotTableEntry(int apiVersion, EventHandler handler)
                : base(apiVersion, handler)
            {
                mEntries = new CountedOffsetItemList<ActorSlotEntry>(handler);
            }
            public ActorSlotTableEntry(int apiVersion, EventHandler handler, Stream s) : base(apiVersion, handler, s) { }

            public ActorSlotTableEntry(int apiVersion, EventHandler handler, ActorSlotTableEntry basis)
                : base(apiVersion, handler, basis)
            {
            }

            public string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < mEntries.Count; i++)
                    {
                        sb.AppendFormat("[{0:00}]{1}\n", i, mEntries[i].Value);
                    }
                    return sb.ToString();
                }
            }
            public CountedOffsetItemList<ActorSlotEntry> Entries
            {
                get { return mEntries; }
                set { mEntries = value; OnElementChanged(); }
            }

            protected override void Parse(Stream s)
            {
                BinaryReader br = new BinaryReader(s);
                UInt32 padding = br.ReadUInt32(); //7E7E7E7E padding
                mEntries = new CountedOffsetItemList<ActorSlotEntry>(handler, s);
            }

            public override void UnParse(Stream s)
            {
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(new byte[] { 0x7E, 0x7E, 0x7E, 0x7E }); //7E7E7E7E padding
                mEntries.UnParse(s);
            }
            public bool Equals(ActorSlotTableEntry other)
            {
                return base.Equals(other);
            }

        }
        public class ActorSlotEntry : DependentElement, IEquatable<ActorSlotEntry>
        {
            private UInt32 mIndex;
            private string mActorName = String.Empty;
            private string mSlotName = String.Empty;
            public ActorSlotEntry(int apiVersion, EventHandler handler) : base(apiVersion, handler) { }
            public ActorSlotEntry(int apiVersion, EventHandler handler, Stream s) : base(apiVersion, handler, s) { }

            public ActorSlotEntry(int apiVersion, EventHandler handler, ActorSlotEntry basis)
                : base(apiVersion, handler, basis)
            {
            }
            public string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("Index:\t0x{0:X8}\n", mIndex);
                    sb.AppendFormat("Actor:\t{0}\n", mActorName);
                    sb.AppendFormat("Slot:\t{0}\n", mSlotName);
                    return sb.ToString();
                }
            }
            [ElementPriority(1)]
            public uint Index
            {
                get { return mIndex; }
                set { mIndex = value; OnElementChanged(); }
            }
            [ElementPriority(2)]
            public string ActorName
            {
                get { return mActorName; }
                set { mActorName = value; OnElementChanged(); }
            }
            [ElementPriority(3)]
            public string SlotName
            {
                get { return mSlotName; }
                set { mSlotName = value; OnElementChanged(); }
            }

            protected override void Parse(Stream s)
            {
                BinaryReader br = new BinaryReader(s);
                mIndex = br.ReadUInt32();
                mActorName = br.ReadZString(512);
                mSlotName = br.ReadZString(512);
            }

            public override void UnParse(Stream s)
            {
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(mIndex);
                bw.WriteZString(mActorName, 0x23, 512);
                bw.WriteZString(mSlotName, 0x23, 512);
            }
            public override string ToString()
            {
                return String.Format("{0:X8}:{1},{2}", mIndex, mActorName, mSlotName);
            }

            public bool Equals(ActorSlotEntry other)
            {
                return mIndex.Equals(other.mIndex) && mActorName.Equals(other.mActorName) && mSlotName.Equals(other.mSlotName);
            }
        }
        #endregion

        #region Events
        public class EventTable : DependentElement
        {
            private UInt32 mVersion;
            private EventList mEvents;
            public EventTable(int apiVersion, EventHandler handler)
                : base(apiVersion, handler)
            {
                mEvents = new EventList(handler);
            }
            public EventTable(int apiVersion, EventHandler handler, Stream s) : base(apiVersion, handler, s) { }
            public EventTable(int apiVersion, EventHandler handler, EventTable basis)
                : base(apiVersion, handler, basis)
            {
            }

            public string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("Version:\t0x{0:X8}\n", mVersion);
                    sb.AppendFormat("Events:\n");
                    for (int i = 0; i < mEvents.Count; i++)
                    {
                        sb.AppendFormat("==Event[{0}]==\n{1}\n", i, mEvents[i].Value);
                    }
                    return sb.ToString();
                }
            }
            [ElementPriority(1)]
            public uint Version
            {
                get { return mVersion; }
                set { mVersion = value; OnElementChanged(); }
            }
            [ElementPriority(2)]
            public EventList Events
            {
                get { return mEvents; }
                set { mEvents = value; OnElementChanged(); }
            }


            protected override void Parse(Stream s)
            {
                BinaryReader br = new BinaryReader(s);
                string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != "=CE=")
                    throw new Exception(String.Format("Bad ClipEvent header: Expected \"=CE=\", but got {0}", magic));
                mVersion = br.ReadUInt32();
                mEvents = new EventList(handler, s);
            }


            public override void UnParse(Stream s)
            {
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(Encoding.ASCII.GetBytes("=CE="));
                bw.Write(mVersion);
                mEvents.UnParse(s);

            }
        }
        public class EventList : DependentElementList<Event>
        {

            public EventList(EventHandler handler) : base(handler) { }
            public EventList(EventHandler handler, Stream s) : base(handler, s) { }
            public override bool Add(params object[] fields)
            {
                if (fields.Length == 0) return false;
                if (fields.Length == 1 && typeof(Event).IsAssignableFrom(fields[0].GetType()))
                {
                    ((IList<Event>)this).Add((Event)fields[0]);
                    return true;
                }
                Add(Event.CreateInstance(0, this.handler, (ClipEventType)(int)fields[0]));
                return true;
            }
            protected override void Parse(Stream s)
            {
                BinaryReader br = new BinaryReader(s);
                uint count = ReadCount(s);
                long endOffset = br.ReadUInt32() + 4 + s.Position;
                long startOffset = br.ReadUInt32();
                if (checking && count > 0 && startOffset != 4)
                    throw new Exception(String.Format("Expected startOffset of 4 at =CE= section, but got 0x{0:X8}", startOffset));
                for (uint i = 0; i < count; i++) { ((IList<Event>)this).Add(CreateElement(s)); }
                if (checking && s.Position != endOffset)
                    throw new Exception(String.Format("Expected endOffset of 0x{0:X8} at =CE= section, but got 0x{1:X8}", endOffset, s.Position));
            }
            public override void UnParse(Stream s)
            {
                BinaryWriter bw = new BinaryWriter(s);
                WriteCount(s, (uint)base.Count);
                long offsetPos = s.Position;
                bw.Write(0);
                bw.Write(base.Count > 0 ? 4 : 0);
                long startPos = s.Position;
                for (int i = 0; i < base.Count; i++) { WriteElement(s, this[i]); }
                long endPos = s.Position;
                uint size = (uint)(endPos - startPos);
                s.Seek(offsetPos, SeekOrigin.Begin);
                bw.Write(size);
                s.Seek(endPos, SeekOrigin.Begin);


            }
            protected override Event CreateElement(Stream s)
            {
                BinaryReader br = new BinaryReader(s);
                ClipEventType type = (ClipEventType)br.ReadUInt16();
                return Event.CreateInstance(0, handler, type, s);
            }
            protected override void WriteElement(Stream s, Event element)
            {
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write((ushort)element.Type);
                base.WriteElement(s, element);
            }
        }
        public abstract class Event : DependentElement, IEquatable<Event>
        {

            protected Event(int apiVersion, EventHandler handler, ClipEventType type)
                : base(apiVersion, handler)
            {
                mType = type;
                mFloat01 = -1f;
                mFloat02 = -2f;
                mShort01 = 0xC1E4;
            }
            protected Event(int apiVersion, EventHandler handler, ClipEventType type, Stream s)
                : this(apiVersion, handler,type)
            {
                if (s != null) Parse(s);
            }
            protected Event(int apiVersion, EventHandler handler, Event basis)
                : base(apiVersion, handler)
            {
                mType = basis.Type;
            }
            private ClipEventType mType;
            private UInt16 mShort01;
            private UInt32 mId;
            private Single mTimecode;
            private Single mFloat01;
            private Single mFloat02;
            private UInt32 mInt01;
            private String mEventName = String.Empty;

            public virtual string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("Type:\t{0}\n", mType);
                    sb.AppendFormat("Unknown01:\t0x{0:X4}\n", mShort01);
                    sb.AppendFormat("Id:\t0x{0:X4}\n", mId);
                    sb.AppendFormat("Timecode:\t{0,8:0.00000}\n", mTimecode);
                    sb.AppendFormat("Float01:\t{0,8:0.00000}\n", mFloat01);
                    sb.AppendFormat("Float02:\t{0,8:0.00000}\n", mFloat02);
                    sb.AppendFormat("Unknown02:\t0x{0:X8}\n", mInt01);
                    sb.AppendFormat("Event Name:\t{0}\n", mEventName);
                    return sb.ToString();
                }
            }
            [ElementPriority(0)]
            public ClipEventType Type
            {
                get { return mType; }
            }
            [ElementPriority(1)]
            public string EventName
            {
                get { return mEventName; }
                set { mEventName = value; OnElementChanged(); }
            }
            [ElementPriority(2)]
            public float Float01
            {
                get { return mFloat01; }
                set { mFloat01 = value; OnElementChanged(); }
            }
            [ElementPriority(3)]
            public float Float02
            {
                get { return mFloat02; }
                set { mFloat02 = value; OnElementChanged(); }
            }
            [ElementPriority(4)]
            public uint Id
            {
                get { return mId; }
                set { mId = value; OnElementChanged(); }
            }
            [ElementPriority(5)]
            public uint Int01
            {
                get { return mInt01; }
                set { mInt01 = value; OnElementChanged(); }
            }
            [ElementPriority(6)]
            public ushort Short01
            {
                get { return mShort01; }
                set { mShort01 = value; OnElementChanged(); }
            }
            [ElementPriority(7)]
            public float Timecode
            {
                get { return mTimecode; }
                set { mTimecode = value; OnElementChanged(); }
            }
            public static Event CreateInstance(int apiVersion, EventHandler handler, ClipEventType type)
            {
                return CreateInstance(apiVersion, handler, type, null);
            }

            public static Event CreateInstance(int apiVersion, EventHandler handler, ClipEventType type, Stream s)
            {
                switch (type)
                {
                    case ClipEventType.Parent: return new ParentEvent(apiVersion, handler, type, s);
                    case ClipEventType.DestroyProp: return new DestroyPropEvent(apiVersion, handler, type, s);
                    case ClipEventType.Effect: return new EffectEvent(apiVersion, handler, type, s);
                    case ClipEventType.Sound: return new SoundEvent(apiVersion, handler, type, s);
                    case ClipEventType.Script: return new ScriptEvent(apiVersion, handler, type, s);
                    case ClipEventType.Visibility: return new VisibilityEvent(apiVersion, handler, type, s);
                    case ClipEventType.StopEffect: return new StopEffectEvent(apiVersion, handler, type, s);
                    case ClipEventType.UnParent: return new UnparentEvent(apiVersion, handler, type, s);
                    default: throw new NotImplementedException(String.Format("Event type: {0} not implemented", type));
                }
            }
            protected override void Parse(Stream s)
            {
                BinaryReader br = new BinaryReader(s);
                mShort01 = br.ReadUInt16();
                mId = br.ReadUInt32();
                mTimecode = br.ReadSingle();
                mFloat01 = br.ReadSingle();
                mFloat02 = br.ReadSingle();
                mInt01 = br.ReadUInt32();
                uint strlen = br.ReadUInt32();
                mEventName = br.ReadZString();
                while ((s.Position % 4) != 0) br.ReadByte(); //padding to next DWORD
            }
            public override void UnParse(Stream s)
            {
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(mShort01);
                bw.Write(mId);
                bw.Write(mTimecode);
                bw.Write(mFloat01);
                bw.Write(mFloat02);
                bw.Write(mInt01);
                bw.Write(mEventName.Length);
                bw.WriteZString(mEventName);
                while ((s.Position % 4) != 0) bw.Write((byte)0x00); //padding to next DWORD
            }
            public override string ToString()
            {
                return mType.ToString();
            }

            public bool Equals(Event other)
            {
                return base.Equals(other);
            }
        }
        [ConstructorParameters(new object[] { ClipEventType.Parent })]
        public class ParentEvent : Event
        {
            internal ParentEvent(int apiVersion, EventHandler handler, ClipEventType type, Stream s)
                : base(apiVersion, handler, type)
            {
                 mMatrix4x4 = new Single[16];
                 if (s != null) Parse(s);
            }
            public ParentEvent(int apiVersion, EventHandler handler, ParentEvent basis)
                : base(apiVersion, handler, basis)
            {
            }

            private UInt32 mActorNameHash;
            private UInt32 mObjectNameHash;
            private UInt32 mSlotNameHash;
            private UInt32 mUnknown01;
            private Single[] mMatrix4x4;

            public override string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder(base.Value);
                    sb.AppendFormat("Actor:\t0x{0:X8}\n", mActorNameHash);
                    sb.AppendFormat("Object:\t0x{0:X8}\n", mObjectNameHash);
                    sb.AppendFormat("Slot:\t0x{0:X8}\n", mSlotNameHash);
                    sb.AppendFormat("Unknown01:\t0x{0:X8}\n", mUnknown01);
                    sb.AppendFormat("Matrix:\n");
                    sb.AppendFormat("[{0,8:0.00000},{1,8:0.00000},{2,8:0.00000},{3,8:0.00000}]\n"
                        , mMatrix4x4[0], mMatrix4x4[1], mMatrix4x4[2], mMatrix4x4[3]);
                    sb.AppendFormat("[{0,8:0.00000},{1,8:0.00000},{2,8:0.00000},{3,8:0.00000}]\n"
                        , mMatrix4x4[4], mMatrix4x4[5], mMatrix4x4[6], mMatrix4x4[7]);
                    sb.AppendFormat("[{0,8:0.00000},{1,8:0.00000},{2,8:0.00000},{3,8:0.00000}]\n"
                        , mMatrix4x4[8], mMatrix4x4[9], mMatrix4x4[10], mMatrix4x4[11]);
                    sb.AppendFormat("[{0,8:0.00000},{1,8:0.00000},{2,8:0.00000},{3,8:0.00000}]\n"
                        , mMatrix4x4[12], mMatrix4x4[13], mMatrix4x4[14], mMatrix4x4[15]);
                    return sb.ToString();
                }
            }
            [ElementPriority(8)]
            public uint ActorNameHash
            {
                get { return mActorNameHash; }
                set { mActorNameHash = value; OnElementChanged(); }
            }
            [ElementPriority(9)]
            public uint ObjectNameHash
            {
                get { return mObjectNameHash; }
                set { mObjectNameHash = value; OnElementChanged(); }
            }
            [ElementPriority(10)]
            public uint SlotNameHash
            {
                get { return mSlotNameHash; }
                set { mSlotNameHash = value; OnElementChanged(); }
            }
            [ElementPriority(11)]
            public uint Unknown01
            {
                get { return mUnknown01; }
                set { mUnknown01 = value; OnElementChanged(); }
            }
            [ElementPriority(12)]
            public float[] Matrix4X4
            {
                get { return mMatrix4x4; }
                set { mMatrix4x4 = value; OnElementChanged(); }
            }
            protected override void Parse(Stream s)
            {
                base.Parse(s);
                BinaryReader br = new BinaryReader(s);
                mActorNameHash = br.ReadUInt32();
                mObjectNameHash = br.ReadUInt32();
                mSlotNameHash = br.ReadUInt32();
                mUnknown01 = br.ReadUInt32();
                mMatrix4x4 = new Single[16];
                for (int i = 0; i < 16; i++) { mMatrix4x4[i] = br.ReadSingle(); }
            }
            public override void UnParse(Stream s)
            {
                base.UnParse(s);
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(mActorNameHash);
                bw.Write(mObjectNameHash);
                bw.Write(mSlotNameHash);
                bw.Write(mUnknown01);
                for (int i = 0; i < 16; i++) bw.Write(mMatrix4x4[i]);
            }
        }
        [ConstructorParameters(new object[] { ClipEventType.UnParent })]
        public class UnparentEvent : Event
        {
            internal UnparentEvent(int apiVersion, EventHandler handler, ClipEventType type, Stream s) : base(apiVersion, handler, type, s) { }

            public UnparentEvent(int apiVersion, EventHandler handler, UnparentEvent basis)
                : base(apiVersion, handler, basis)
            {
            }
            private UInt32 mObjectNameHash;

            public override string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder(base.Value);
                    sb.AppendFormat("Object:\t0x{0:X8}\n", mObjectNameHash);
                    return sb.ToString();
                }
            }
            [ElementPriority(8)]
            public uint ObjectNameHash
            {
                get { return mObjectNameHash; }
                set { mObjectNameHash = value; OnElementChanged(); }
            }

            protected override void Parse(Stream s)
            {
                base.Parse(s);
                BinaryReader br = new BinaryReader(s);
                mObjectNameHash = br.ReadUInt32();
            }
            public override void UnParse(Stream s)
            {
                base.UnParse(s);
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(mObjectNameHash);
            }
        }
        [ConstructorParameters(new object[] { ClipEventType.Sound })]
        public class SoundEvent : Event
        {
            internal SoundEvent(int apiVersion, EventHandler handler, ClipEventType type, Stream s) : base(apiVersion, handler, type, s) { }

            public SoundEvent(int apiVersion, EventHandler handler, SoundEvent basis)
                : base(apiVersion, handler, basis)
            {
            }
            private String mSoundName;

            public override string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder(base.Value);
                    sb.AppendFormat("Sound Name:\t{0}\n", mSoundName);
                    return sb.ToString();
                }
            }
            [ElementPriority(8)]
            public string SoundName
            {
                get { return mSoundName; }
                set { mSoundName = value; OnElementChanged(); }
            }

            protected override void Parse(Stream s)
            {
                base.Parse(s);
                BinaryReader br = new BinaryReader(s);
                mSoundName = br.ReadZString(128);
            }
            public override void UnParse(Stream s)
            {
                base.UnParse(s);
                BinaryWriter bw = new BinaryWriter(s);
                bw.WriteZString(mSoundName, 0x00, 128);
            }
        }
        [ConstructorParameters(new object[] { ClipEventType.Script })]
        public class ScriptEvent : Event
        {
            internal ScriptEvent(int apiVersion, EventHandler handler, ClipEventType type, Stream s) : base(apiVersion, handler, type, s) { }
            public ScriptEvent(int apiVersion, EventHandler handler, ScriptEvent basis)
                : base(apiVersion, handler, basis)
            {
            }
        }

        [ConstructorParameters(new object[] { ClipEventType.Effect })]
        public class EffectEvent : Event
        {
            internal EffectEvent(int apiVersion, EventHandler handler, ClipEventType type, Stream s) : base(apiVersion, handler, type, s) { }
            public EffectEvent(int apiVersion, EventHandler handler, EffectEvent basis)
                : base(apiVersion, handler, basis)
            {
            }
            private UInt32 mUnknown01;
            private UInt32 mUnknown02;
            private UInt32 mEffectNameHash;
            private UInt32 mActorNameHash;
            private UInt32 mSlotNameHash;
            private UInt32 mUnknown03;

            public override string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder(base.Value);
                    sb.AppendFormat("Unknown01:\t0x{0:X8}\n", mUnknown01);
                    sb.AppendFormat("Unknown02:\t0x{0:X8}\n", mUnknown02);
                    sb.AppendFormat("Effect:\t0x{0:X8}\n", mEffectNameHash);
                    sb.AppendFormat("Actor:\t0x{0:X8}\n", mActorNameHash);
                    sb.AppendFormat("Slot:\t0x{0:X8}\n", mSlotNameHash);
                    sb.AppendFormat("Unknown03:\t0x{0:X8}\n", mUnknown03);
                    return sb.ToString();
                }
            }
            [ElementPriority(8)]
            public uint Unknown01
            {
                get { return mUnknown01; }
                set { mUnknown01 = value; OnElementChanged(); }
            }
            [ElementPriority(9)]
            public uint Unknown02
            {
                get { return mUnknown02; }
                set { mUnknown02 = value; OnElementChanged(); }
            }
            [ElementPriority(10)]
            public uint EffectNameHash
            {
                get { return mEffectNameHash; }
                set { mEffectNameHash = value; OnElementChanged(); }
            }
            [ElementPriority(11)]
            public uint ActorNameHash
            {
                get { return mActorNameHash; }
                set { mActorNameHash = value; OnElementChanged(); }
            }
            [ElementPriority(12)]
            public uint SlotNameHash
            {
                get { return mSlotNameHash; }
                set { mSlotNameHash = value; OnElementChanged(); }
            }
            [ElementPriority(13)]
            public uint Unknown03
            {
                get { return mUnknown03; }
                set { mUnknown03 = value; OnElementChanged(); }
            }

            protected override void Parse(Stream s)
            {
                base.Parse(s);
                BinaryReader br = new BinaryReader(s);
                mUnknown01 = br.ReadUInt32();
                mUnknown02 = br.ReadUInt32();
                mEffectNameHash = br.ReadUInt32();
                mActorNameHash = br.ReadUInt32();
                mSlotNameHash = br.ReadUInt32();
                mUnknown03 = br.ReadUInt32();
            }
            public override void UnParse(Stream s)
            {
                base.UnParse(s);
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(mUnknown01);
                bw.Write(mUnknown02);
                bw.Write(mEffectNameHash);
                bw.Write(mActorNameHash);
                bw.Write(mSlotNameHash);
                bw.Write(mUnknown03);
            }

        }
        [ConstructorParameters(new object[] { ClipEventType.Visibility })]
        public class VisibilityEvent : Event
        {
            internal VisibilityEvent(int apiVersion, EventHandler handler, ClipEventType type, Stream s) : base(apiVersion, handler, type, s) { }
            public VisibilityEvent(int apiVersion, EventHandler handler, VisibilityEvent basis)
                : base(apiVersion, handler, basis)
            {
            }
            private Single mVisibility;

            public override string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder(base.Value);
                    sb.AppendFormat("Visibility:\t{0,8:0.00000}\n", mVisibility);
                    return sb.ToString();
                }
            }
            [ElementPriority(8)]
            public float Visibility
            {
                get { return mVisibility; }
                set { mVisibility = value; OnElementChanged(); }
            }

            protected override void Parse(Stream s)
            {
                base.Parse(s);
                BinaryReader br = new BinaryReader(s);
                mVisibility = br.ReadSingle();
            }
            public override void UnParse(Stream s)
            {
                base.UnParse(s);
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(mVisibility);
            }
        }

        [ConstructorParameters(new object[] { ClipEventType.DestroyProp })]
        public class DestroyPropEvent : Event
        {
            internal DestroyPropEvent(int apiVersion, EventHandler handler, ClipEventType type, Stream s) : base(apiVersion, handler, type, s) { }
            public DestroyPropEvent(int apiVersion, EventHandler handler, DestroyPropEvent basis)
                : base(apiVersion, handler, basis)
            {
            }
            private UInt32 mPropNameHash;

            public override string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder(base.Value);
                    sb.AppendFormat("Prop:\t0x{0:X8}\n", mPropNameHash);
                    return sb.ToString();
                }
            }
            [ElementPriority(8)]
            public uint PropNameHash
            {
                get { return mPropNameHash; }
                set { mPropNameHash = value; OnElementChanged(); }
            }

            protected override void Parse(Stream s)
            {
                base.Parse(s);
                BinaryReader br = new BinaryReader(s);
                mPropNameHash = br.ReadUInt32();
            }
            public override void UnParse(Stream s)
            {
                base.UnParse(s);
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(mPropNameHash);
            }
        }

        [ConstructorParameters(new object[] { ClipEventType.StopEffect })]
        public class StopEffectEvent : Event
        {
            private UInt32 mEffectNameHash;
            private UInt32 mUnknown01;
            internal StopEffectEvent(int apiVersion, EventHandler handler, ClipEventType type, Stream s) : base(apiVersion, handler, type, s) { }
            public StopEffectEvent(int apiVersion, EventHandler handler, StopEffectEvent basis)
                : base(apiVersion, handler, basis)
            {
            }

            public override string Value
            {
                get
                {
                    StringBuilder sb = new StringBuilder(base.Value);
                    sb.AppendFormat("Effect:\t0x{0:X8}\n", mEffectNameHash);
                    sb.AppendFormat("Unknown01:\t0x{0:X8}\n", mUnknown01);
                    return sb.ToString();
                }
            }
            [ElementPriority(8)]
            public uint EffectNameHash
            {
                get { return mEffectNameHash; }
                set { mEffectNameHash = value; OnElementChanged(); }
            }
            [ElementPriority(9)]
            public uint Unknown01
            {
                get { return mUnknown01; }
                set { mUnknown01 = value; OnElementChanged(); }
            }

            protected override void Parse(Stream s)
            {
                base.Parse(s);
                BinaryReader br = new BinaryReader(s);
                mEffectNameHash = br.ReadUInt32();
                mUnknown01 = br.ReadUInt32();
            }
            public override void UnParse(Stream s)
            {
                base.UnParse(s);
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(mEffectNameHash);
                bw.Write(mUnknown01);
            }

        }
        #endregion
        public class ClipEndSection : DependentElement
        {
            private Single mX;
            private Single mY;
            private Single mZ;
            private Single mW;

            public ClipEndSection(int apiVersion, EventHandler handler)
                : base(apiVersion, handler)
            {
            }

            public ClipEndSection(int apiVersion, EventHandler handler, Stream s)
                : base(apiVersion, handler, s)
            {
            }

            public ClipEndSection(int apiVersion, EventHandler handler, ClipEndSection basis)
                : base(apiVersion, handler, basis)
            {
            }
            [ElementPriority(1)]
            public float X
            {
                get { return mX; }
                set { mX = value; OnElementChanged(); }
            }
            [ElementPriority(2)]
            public float Y
            {
                get { return mY; }
                set { mY = value; OnElementChanged(); }
            }
            [ElementPriority(3)]
            public float Z
            {
                get { return mZ; }
                set { mZ = value; OnElementChanged(); }
            }
            [ElementPriority(4)]
            public float W
            {
                get { return mW; }
                set { mW = value; OnElementChanged(); }
            }

            public string Value
            {
                get { return String.Format("[{0,8:0.00000},{1,8:0.00000},{2,8:0.00000},{2,8:0.00000}]",mX,mY,mZ,mW); }
            }
            protected override void Parse(Stream s)
            {
                BinaryReader br = new BinaryReader(s);
                mX = br.ReadSingle();
                mY = br.ReadSingle();
                mZ = br.ReadSingle();
                mW = br.ReadSingle();
            }

            public override void UnParse(Stream s)
            {
                BinaryWriter bw = new BinaryWriter(s);
                bw.Write(mX);
                bw.Write(mY);
                bw.Write(mZ);
                bw.Write(mW);
            }
        }
        #region Constructors
        public ClipResource(int apiVersion, Stream s)
            : base(apiVersion, s)
        {
            mS3Clip = new byte[0];
            mActorSlotTable = new ActorSlotTable(0, this.OnResourceChanged);
            mEventSectionTable = new EventTable(0, this.OnResourceChanged);
            mEndSection = new ClipEndSection(0, this.OnResourceChanged);

            if (base.stream == null)
            {
                base.stream = this.UnParse();
                this.OnResourceChanged(this, new EventArgs());
            }
            base.stream.Position = 0L;
            Parse(s);
        }
        #endregion

        #region Fields
        private UInt32 mUnknown01;
        private UInt32 mUnknown02;
        private byte[] mS3Clip;
        //private S3Clip mClip;
        private ActorSlotTable mActorSlotTable;
        private string mActorName;
        private EventTable mEventSectionTable;
        private ClipEndSection mEndSection;
        #endregion

        #region I/O
        public string Value
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("Unknown01:\t0x{0:X8}\n", mUnknown01);
                sb.AppendFormat("Unknown02:\t0x{0:X8}\n", mUnknown02);
                sb.AppendFormat("Actor/Slot Table:\n{0}\n", mActorSlotTable.Value);
                sb.AppendFormat("Actor:\t{0}\n", mActorName);
                sb.AppendFormat("Event Table:\n{0}\n", mEventSectionTable.Value);
                sb.AppendFormat("End Section:\n{0}\n", mEndSection.Value);
                return sb.ToString();

            }
        }
        [ElementPriority(1)]
        public uint Unknown01
        {
            get { return mUnknown01; }
            set { mUnknown01 = value; OnResourceChanged(this, new EventArgs()); }
        }
        [ElementPriority(2)]
        public uint Unknown02
        {
            get { return mUnknown02; }
            set { mUnknown02 = value; OnResourceChanged(this, new EventArgs()); }
        }
        [ElementPriority(3)]
        public BinaryReader S3Clip
        {
            get
            {
                MemoryStream s = new MemoryStream(mS3Clip);
                s.Position = 0L;
                return new BinaryReader(s);
            }
            set
            {
                if (value.BaseStream.CanSeek)
                {
                    value.BaseStream.Position = 0L;
                    mS3Clip = value.ReadBytes((int)value.BaseStream.Length);
                }
                else
                {
                    MemoryStream s = new MemoryStream();
                    byte[] buffer = new byte[0x100000];
                    for (int i = value.BaseStream.Read(buffer, 0, buffer.Length); i > 0; i = value.BaseStream.Read(buffer, 0, buffer.Length))
                    {
                        s.Write(buffer, 0, i);
                    }
                    mS3Clip = new BinaryReader(s).ReadBytes((int)s.Length);
                }
                OnResourceChanged(this, new EventArgs());
            }
        }
        [ElementPriority(4)]
        public ActorSlotTable ActorSlots
        {
            get { return mActorSlotTable; }
            set { mActorSlotTable = value; OnResourceChanged(this, new EventArgs()); }
        }
        [ElementPriority(5)]
        public string ActorName
        {
            get { return mActorName; }
            set { mActorName = value; OnResourceChanged(this, new EventArgs()); }
        }
        [ElementPriority(6)]
        public EventTable EventSection
        {
            get { return mEventSectionTable; }
            set { mEventSectionTable = value; OnResourceChanged(this, new EventArgs()); }
        }
        [ElementPriority(7)]
        public ClipEndSection EndSection
        {
            get { return mEndSection; }
            set { mEndSection = value; OnResourceChanged(this, new EventArgs()); }
        }
        //[ElementPriority(8)]
        //[DataGridExpandable(true)]
        //public S3Clip CLIP
        //{
        //    get { return mClip; }
        //    set { mClip = value; OnResourceChanged(this, new EventArgs()); }
        //}

        private void Parse(Stream s)
        {
            BinaryReader br = new BinaryReader(s);

            //header
            if (br.ReadUInt32() != 0x6B20C4F3) throw new Exception("Not a valid CLIP resource");
            long linkedClipOffset = br.ReadUInt32();
            long clipSize = br.ReadUInt32();
            long clipOffset = br.ReadUInt32() + s.Position - 4;
            long slotOffset = br.ReadUInt32() + s.Position - 4;
            long actorOffset = br.ReadUInt32() + s.Position - 4;
            long eventOffset = br.ReadUInt32() + s.Position - 4;
            mUnknown01 = br.ReadUInt32();
            mUnknown02 = br.ReadUInt32();
            long endOffset = br.ReadUInt32() + s.Position - 4;


            s.Seek(clipOffset, SeekOrigin.Begin);
            mS3Clip = new byte[(int)clipSize];
            mS3Clip = br.ReadBytes((int)clipSize);
            //mClip = new s3piwrappers.S3Clip(0, this.OnResourceChanged, new MemoryStream(mS3Clip));

            s.Seek(slotOffset, SeekOrigin.Begin);
            mActorSlotTable = new ActorSlotTable(0, this.OnResourceChanged, s);

            s.Seek(actorOffset, SeekOrigin.Begin);
            mActorName = br.ReadZString();

            s.Seek(eventOffset, SeekOrigin.Begin);
            mEventSectionTable = new EventTable(0, this.OnResourceChanged, s);

            s.Seek(endOffset, SeekOrigin.Begin);
            mEndSection = new ClipEndSection(0, this.OnResourceChanged, s);
        }
        protected override Stream UnParse()
        {
            MemoryStream s = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(s);
            bw.Write(0x6B20C4F3);
            long mainOffsetList = s.Position;
            long clipSize = 0;
            long clipOffset = 0;
            long slotOffset = 0;
            long actorOffset = 0;
            long eventOffset = 0;
            long endOffset = 0;
            s.Seek(52, SeekOrigin.Current);

            clipSize = mS3Clip.Length;
            clipOffset = s.Position;
            bw.Write(mS3Clip);
            while ((s.Position % 4) != 0) bw.Write((byte)0x7e); //padding to next dword

            slotOffset = s.Position;
            mActorSlotTable.UnParse(s);
            while ((s.Position % 4) != 0) bw.Write((byte)0x7e); //padding to next dword

            actorOffset = s.Position;
            bw.WriteZString(mActorName);
            while ((s.Position % 4) != 0) bw.Write((byte)0x7e); //padding to next dword

            eventOffset = s.Position;
            mEventSectionTable.UnParse(s);
            while ((s.Position % 4) != 0) bw.Write((byte)0x7e); //padding to next dword


            endOffset = s.Position;
            mEndSection.UnParse(s);

            //write header last
            s.Seek(mainOffsetList, SeekOrigin.Begin);
            bw.Write((uint)(0));
            bw.Write((uint)clipSize);
            bw.Write((uint)(clipOffset - s.Position));
            bw.Write((uint)(slotOffset - s.Position));
            bw.Write((uint)(actorOffset - s.Position));
            bw.Write((uint)(eventOffset - s.Position));
            bw.Write(mUnknown01);
            bw.Write(mUnknown02);
            bw.Write((uint)(endOffset - s.Position));
            bw.Write(new byte[16]);
            s.Position = s.Length;
            return s;
        }
        #endregion

        #region s3pi
        public override int RecommendedApiVersion
        {
            get { return kRecommendedApiVersion; }
        }
        static bool checking = s3pi.Settings.Settings.Checking;
        const int kRecommendedApiVersion = 1;
        #endregion

    }
}