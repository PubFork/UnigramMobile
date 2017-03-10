// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Messages
{
	/// <summary>
	/// RCP method messages.reorderStickerSets.
	/// Returns <see cref="Telegram.Api.TL.TLBoolBase"/>
	/// </summary>
	public partial class TLMessagesReorderStickerSets : TLObject
	{
		[Flags]
		public enum Flag : Int32
		{
			Masks = (1 << 0),
		}

		public bool IsMasks { get { return Flags.HasFlag(Flag.Masks); } set { Flags = value ? (Flags | Flag.Masks) : (Flags & ~Flag.Masks); } }

		public Flag Flags { get; set; }
		public TLVector<Int64> Order { get; set; }

		public TLMessagesReorderStickerSets() { }
		public TLMessagesReorderStickerSets(TLBinaryReader from)
		{
			Read(from);
		}

		public override TLType TypeId { get { return TLType.MessagesReorderStickerSets; } }

		public override void Read(TLBinaryReader from)
		{
			Flags = (Flag)from.ReadInt32();
			Order = TLFactory.Read<TLVector<Int64>>(from);
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x78337739);
			to.Write((Int32)Flags);
			to.WriteObject(Order);
		}
	}
}