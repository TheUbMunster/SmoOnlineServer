using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ENet;

namespace Shared.VoiceProxNetworking
{
   /// <summary>
   /// <b>Author: TheUbMunster, Raphael Kwankam</b><br/><br/>
   /// </summary>
   public static class RPC
   {
      private static Dictionary<string, (Delegate, bool passConnection)> handlers = new Dictionary<string, (Delegate, bool passConnection)>();
      private static JsonSerializerOptions jsonOptions = new();

      static RPC()
      {
         RPC.jsonOptions.IncludeFields = true;
      }

      /// <summary>
      /// Registers a method handler, and couples it to the delegate's name<br>
      /// This supports static functions and object member functions<br>
      /// Passing anonymous functions, e.g. lambdas, is NOT supported. It'll compile and run,
      /// but client computers won't know the lambda's name and therefore cannot call it. <br>
      /// 
      /// Note: this tries to respect inheritance. If you pass in ConcreteObj.DoThing() which overrides
      /// AbstractObj.DoThing(), it will couple the handler to AbstractObj.DoThing(). 
      /// </summary>
      /// <param name="handler">StaticFunction, or Object.MemberFunction</param>
      public static void AddMethodHandler(Delegate handler, bool passConnectionObjectAsFirstParameter = false)
      {
         string name = GetMethodName(handler.Method);

         Console.WriteLine("Registering method handler with name \"" + name + "\"");

         handlers.Add(name, (handler, passConnectionObjectAsFirstParameter));
      }

      /// <summary>
      /// Starts listending to RPC invocations on connection
      /// </summary>
      public static void Listen(Peer conn) //is peer the equivalent of connection?
      {
         conn.RegisterRawDataHandler("RPC_Protocol", (rawData, con) =>
         {
            try
            {
               Console.WriteLine("Got RPC packet");
               BasePacket? pack = Protocol.Deserialize(rawData.Data, jsonOptions);

               if (pack != null)
                  Protocol.Handle(pack, conn);
               else Console.WriteLine("Recieved null packet");
            }
            catch (Exception e)
            {
               Console.WriteLine("RPC protocol exception\n:" + e);
            }
         });

         Console.WriteLine("Listening for RPC calls...");
      }

      /// <summary>
      /// Calls the specified function on the computer at the other end of the connection<br>
      /// Assumes that the other computer has assigned a handler for the function already;
      /// if not, this will just time out.<br>
      /// </summary>
      public async static Task<object?> RemoteInvoke(Connection connection, Delegate func, params object?[] args)
      {
         return await RemoteInvoke(connection, func.Method, args);
      }

      /// <summary>
      /// Calls classType.funcName via RPC<br>
      /// This method is needed to call member functions without instantiating an object<br>
      /// Ex: If I want to call MyClass.DoThing(args...), C# won't let you do RemoteInvoke(conn, MyClass.DoThing, args...)<br>
      /// Instead, you can do RemoteInvoke(conn, typeof(MyClass), "DoThing", args...)
      /// </summary>
      public async static Task<object?> RemoteInvoke(Connection connection, Type classType, string funcName, params object?[] args)
      {
         MethodInfo? func = classType.GetMethod(funcName);

         if (func is null) throw new ArgumentException("RPC error: Couldn't find function \"" + funcName + "\" inside type " + classType.Name + " via reflection");

         return await RemoteInvoke(connection, func, args);
      }

      /// <summary>
      /// Does RPC directly via MethodInfo<br>
      /// Other RemoteInvokes are convenience methods to automatically derive the MethodInfo with nicer end-user syntax
      /// </summary>
      public async static Task<object?> RemoteInvoke(Connection connection, MethodInfo func, params object?[] args)
      {
         Type retType = func.ReturnType;
         bool hasReturn = retType != typeof(void);
         string? returnName = hasReturn ? retType.FullName : null;
         string? returnChannel = hasReturn ? "RPC_Result_" + rpcTimesInvoked++ : null;

         RPCPacket p = new()
         {
            MethodName = GetMethodName(func),
            Arguments = args,
            ArgumentTypesFullNames = null,
            ReturnTypeFullName = returnName,
            ReturnChannel = returnChannel
         };

         if (!hasReturn)
         {
            RemoteInvokeVoid(connection, p);
            return null;
         }
         else
         {
            object? ret = await RemoteInvokeWithReturn(connection, p, func.ReturnType);

            if (ret != null && !ret.GetType().IsAssignableTo(retType))
               throw new ArgumentException("RemoteInvoke: expected return type " + retType.FullName + ", got inassignable type " + ret.GetType().FullName);

            return ret;
         }
      }

      /// <summary>
      /// Converts an object of type T into JSON<br/>
      /// RPC already needs to know how to serialize all of our types, especially any special cases
      /// (like [Redacted]), so may as well expose this
      /// </summary>
      public static string PublicToJson<T>(T obj)
      {
         return JsonSerializer.Serialize<T>(obj, jsonOptions);
      }


      /// <summary>
      /// Converts a JSON string into an object of type T<br/>
      /// RPC already needs to know how to deserialize all of our types, especially any special cases
      /// (like [Redacted]), so may as well expose this
      /// </summary>
      public static T? PublicFromJson<T>(string str)
      {
         return JsonSerializer.Deserialize<T>(str, jsonOptions);
      }


      public static IList<Type> GetMethodTypes(string methodName)
      {
         return handlers[methodName].Item1.Method.GetParameters().Skip(handlers[methodName].passConnection ? 1 : 0).Select(x => x.ParameterType).ToList();
      }

      public static void LocalInvoke(Connection connection, RPCPacket packet)
      {
         try
         {
            if (packet.ReturnChannel == null)
               LocalInvokeVoid(connection, packet);
            else
               LocalInvokeReturn(connection, packet);
         }
         catch (Exception e)
         {
            Console.WriteLine("RPC LocalInvoke threw exception:\n" + e);
            //todo: send RPC error response packet
         }
      }

      private static string GetMethodName(MethodInfo m)
      {
         MethodInfo abstractBaseMethod = m.GetBaseDefinition();
         return abstractBaseMethod.ReflectedType?.Name + "." + abstractBaseMethod.Name;
      }

      private static void LocalInvokeVoid(Connection connection, RPCPacket packet)
      {
         if (handlers.ContainsKey(packet.MethodName))
         {
            Console.WriteLine("Locally invoking \"" + packet.MethodName + "\"");
            if (handlers[packet.MethodName].passConnection)
            {
               object?[] newArgs = new object?[(packet.Arguments?.Length ?? 0) + 1]; //+1 for connection param
               newArgs[0] = connection;
               if (packet.Arguments != null)
                  Array.Copy(packet.Arguments, 0, newArgs, 1, packet.Arguments.Length);
               handlers[packet.MethodName].Item1.DynamicInvoke(newArgs);
            }
            else
               handlers[packet.MethodName].Item1.DynamicInvoke(packet.Arguments);
         }
         else
            throw new ArgumentException("Error: Attempted to invoke an RPC handler that does not exist/isn't registered.");
      }

      private static void RemoteInvokeVoid(Connection connection, RPCPacket packet)
      {
         Console.WriteLine("Remote invoking void method \"" + packet.MethodName + "\"; not expecting return");

         connection.SendRawData("RPC_Protocol", Protocol.Serialize(packet, jsonOptions));
      }

      private static void LocalInvokeReturn(Connection connection, RPCPacket packet)
      {
         if (handlers.ContainsKey(packet.MethodName))
         {
            Console.WriteLine("Locally invoking \"" + packet.MethodName + "\" and returning");
            object? result = null;
            if (handlers[packet.MethodName].passConnection)
            {
               object?[] newArgs = new object?[(packet.Arguments?.Length ?? 0) + 1]; //+1 for connection param
               newArgs[0] = connection;
               if (packet.Arguments != null)
                  Array.Copy(packet.Arguments, 0, newArgs, 1, packet.Arguments.Length);
               result = handlers[packet.MethodName].Item1.DynamicInvoke(newArgs);
            }
            else
               result = handlers[packet.MethodName].Item1.DynamicInvoke(packet.Arguments);

            connection.SendRawData(packet.ReturnChannel, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result ?? null, result?.GetType() ?? typeof(object), jsonOptions)));

            Console.WriteLine("Return value sent on channel " + packet.ReturnChannel + ":\n" + result + " \"" + packet.ReturnTypeFullName + "\" " + Type.GetType(packet.ReturnTypeFullName!)?.Name);
         }
         else
            throw new ArgumentException("Error: Attempted to invoke an RPC handler that does not exist/isn't registered.");
      }

      private static int rpcTimesInvoked = 0;

      /// <summary>
      /// Calls a function on the machine on the other side of the connection, and returns the result
      /// </summary>
      async private static Task<object?> RemoteInvokeWithReturn(Connection connection, RPCPacket packet, Type returnType)
      {
         const int TIMEOUT_MS =
#if DEBUG
            30000; //30 seconds
#else
            5000; //5 seconds
#endif

         object? res = null;
         SemaphoreSlim returnWait = new(0);

         Console.WriteLine("Remote invoking \"" + packet.MethodName + "\" and expecting return on channel " + packet.ReturnChannel);

         if (packet.ReturnChannel is null || packet.ReturnTypeFullName is null)
            throw new ArgumentException("Malformed input packet in RemoteInvokeAndReturn");

         connection.RegisterRawDataHandler(packet.ReturnChannel, (rawData, con) =>
         {
            res = JsonSerializer.Deserialize(Encoding.UTF8.GetString(rawData.Data), returnType, jsonOptions);

            returnWait.Release();
         });

         connection.SendRawData("RPC_Protocol", Protocol.Serialize(packet, jsonOptions));

         bool success = await returnWait.WaitAsync(TIMEOUT_MS);

         connection.UnRegisterRawDataHandler(packet.ReturnChannel);

         if (!success)
            throw new TimeoutException("Error: RPC call timed out. Did not return in " + TIMEOUT_MS + "ms");

         return res;
      }

   }
}
