using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Reflection;
using Network;

namespace Shared.VoiceProxNetworking
{
   /// <summary>
   /// <b>Author: TheUbMunster & Raphael Kwankam</b><br/><br/>
   /// </summary>
   public static class Protocol
   {
      public enum HandleType
      {
         NoActionTaken,
         FurtherActionNeeded,
         FurtherActionOptional,
         NoActionNeeded
      }

      public static HandleType Handle(BasePacket packet, Connection peer)
      {
         switch (packet)
         {
            case RPCPacket rpc:
               RPC.LocalInvoke(peer, rpc);
               return HandleType.NoActionNeeded;
            case InfoMessagePacket info:
               switch (info.MessageType)
               {
                  case InfoMessagePacket.InfoMessageType.Info:
                     //show in popup? TODO
                     return HandleType.FurtherActionOptional;
                  case InfoMessagePacket.InfoMessageType.Debug:
                     System.Diagnostics.Debug.WriteLine(info.Message);
                     return HandleType.NoActionNeeded;
                  case InfoMessagePacket.InfoMessageType.Error:
                     Console.Error.WriteLine(info.Message); //other party "sent an error" message, should this count as a local exception? probably not.
                     return HandleType.FurtherActionOptional;
                  default:
                     throw new Exception();
               }
            default:
               return HandleType.NoActionTaken;
         }
      }

      #region Serializers
      public static byte[] Serialize<T>(T packet, JsonSerializerOptions options) where T : BasePacket
      {
         return Encoding.UTF8.GetBytes(JsonSerializer.Serialize<T>(packet, options));
      }
      #endregion

      #region Deserializers
      public static BasePacket? Deserialize(byte[] data, JsonSerializerOptions options)
      {
         return Deserialize(Encoding.UTF8.GetString(data), options);
      }

      public static BasePacket? Deserialize(string s, JsonSerializerOptions options)
      {
         //https://www.reddit.com/r/dotnet/comments/w1f920/net_6_rest_api_is_deserializing_property_of/
         //https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to?pivots=dotnet-6-0#support-polymorphic-deserialization
         try
         {
            using (var jDoc = JsonDocument.Parse(s))
            {
               switch (jDoc.RootElement.GetProperty(nameof(BasePacket.CategoricalPacketType)).GetInt32())
               {
                  case (int)BasePacket.PacketType.RPCPacket:
                     {
                        RPCPacket? p = jDoc.RootElement.Deserialize<RPCPacket>(options);
                        if (p != null && p.Arguments != null)
                        {
                           IList<Type> argTypes = RPC.GetMethodTypes(p.MethodName);

                           for (int i = 0; i < p.Arguments.Length; i++)
                           {
                              JsonElement? e = (JsonElement?)p.Arguments[i]; //object gets converted to this during deserialization
                              if (e != null)
                              {
                                 Type t = argTypes[i];

                                 p.Arguments[i] = e.Value.Deserialize(t!, options);
                              }
                              else throw new ArgumentException("Failed to deserialize argument "+i+":\n"+p.Arguments[i]);
                           }
                        }
                        return p;
                     }
                  case (int)BasePacket.PacketType.InfoMessagePacket:
                     {
                        InfoMessagePacket? p = jDoc.RootElement.Deserialize<InfoMessagePacket>(options);
                        return p;
                     }
                  default: return null;
               }
            }
         }
         catch (Exception e)
         {
            Console.WriteLine("Protocol exception. \nPacket: \n"+s+"\nException:\n"+e);
            return null;
         }
      }
      #endregion
   }
}