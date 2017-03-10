// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Channels
{
	/// <summary>
	/// RCP method channels.inviteToChannel.
	/// Returns <see cref="Telegram.Api.TL.TLUpdatesBase"/>
	/// </summary>
	public partial class TLChannelsInviteToChannel : TLObject
	{
		public TLInputChannelBase Channel { get; set; }
		public TLVector<TLInputUserBase> Users { get; set; }

		public TLChannelsInviteToChannel() { }
		public TLChannelsInviteToChannel(TLBinaryReader from)
		{
			Read(from);
		}

		public override TLType TypeId { get { return TLType.ChannelsInviteToChannel; } }

		public override void Read(TLBinaryReader from)
		{
			Channel = TLFactory.Read<TLInputChannelBase>(from);
			Users = TLFactory.Read<TLVector<TLInputUserBase>>(from);
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x199F3A6C);
			to.WriteObject(Channel);
			to.WriteObject(Users);
		}
	}
}