using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shared.VoiceProxNetworking
{
   /// <summary>
   /// <b>Author: TheUbMunster</b><br/><br/>
   /// </summary>
   public class BasePacket
   {
      public enum PacketType
      {
         Undefined = -1,
         DataPacket,
         ActionablePacket, //I think it's useful for this to be distinct from RPC
         RPCPacket,
         InfoMessagePacket
      }

      public PacketType CategoricalPacketType { get; private protected init; }
      protected BasePacket()
      {
         Type tt = GetType();
         if (tt == typeof(BasePacket))
            CategoricalPacketType = PacketType.Undefined; //throw new Exception("Fix me!"); //basepacket ctor was called directly (maybe json deserializer?)
         while (tt!.BaseType != typeof(BasePacket))
            tt = tt.BaseType!; //should be impossible to error, since this code is only executed by implementers?
         string typename = tt.Name; //gets name of direct derived instance
         CategoricalPacketType = Enum.Parse<PacketType>(typename);
      }
   }

   #region PacketCategories
   public class RPCPacket : BasePacket
   {
      public string MethodName { get; init; } = null!;
      protected object?[]? arguments = null;
      public object?[]? Arguments 
      {
         get
         {
            return arguments;
         }
         init 
         {
            arguments = value;
            if (arguments != null)
               ArgumentTypesFullNames ??= arguments.Select(x => x?.GetType().FullName).ToArray();
         }
      } //these need to be serializable
      /// <summary>
      /// <u><i><b>DO NOT ASSIGN TO THIS MANUALLY! IT IS HANDLED AUTOMATICALLY!</b></i></u>
      /// </summary>
      public string?[]? ArgumentTypesFullNames { get; init; } = null;
      public string? ReturnTypeFullName { get; init; } = null;
      public string? ReturnChannel { get; init; } = null;
   }

   public class DataPacket : BasePacket
   {
      public enum DataType
      {
         Undefined = -1,
         TableInstanceDBIPacket,
         UserTextMessagePacket,
         UserListPacket,
         ChatIDPacket
         //...,
      }

      public DataType DatType { get; init; }

      protected DataPacket()
      {
         Type tt = GetType();
         if (tt == typeof(DataPacket))
         {
            DatType = DataType.Undefined;
            return;
         }
         while (tt!.BaseType != typeof(DataPacket))
            tt = tt.BaseType!; //should be impossible to error, since this code is only executed by implementers?
         string typename = tt.Name; //gets name of direct derived instance
         DatType = Enum.Parse<DataType>(typename);
      }
   }

   public class InfoMessagePacket : BasePacket
   {
      public enum InfoMessageType
      {
         Info,
         Debug,
         Error
      }
      public InfoMessageType MessageType { get; init; }
      public string Message { get; init; } = null!;
   }
   #endregion

   #region Data Implementers
   public class TableInstanceDBIPacket : DataPacket
   {
      public enum Purpose
      {
         LoansCache,
         UserTextMessagesCache, //the entire table of text messages
         //...,
      }

      //public [Redacted] Table { get; init; } = null!; //this needs the datatype from the other merge request.
   }

   public class UserTextMessagePacket : DataPacket //just a single text message
    {
        public string sendName { get; init; } = null!;
        public string recName { get; init; } = null!;
        public string content { get; init; } = null!;
        public DateTime timestamp { get; init; }
        public ulong chatID { get; init; }
        //TODO:
        //sender info
        //content
        //timestamp
        //etc, this packet should essentially be equivalent to a row of the user-text messages in the DB (any & all info)
    }

    public class UserListPacket : DataPacket
    {
        public string[] userList { get; init; } = null!;
    }
    public class ChatIDPacket : DataPacket 
    {
        public ulong roomID { get; init; }
        public ulong chatID { get; init; }
    }

    public class UserInformationPacket : DataPacket 
    {
        public string userID { get; init; } = null!;
        public string password { get; init; } = null!;
    }
    #endregion
}