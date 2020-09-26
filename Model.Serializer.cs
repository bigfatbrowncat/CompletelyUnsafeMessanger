using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompletelyUnsafeMessenger
{
    namespace Model
    {
        /// <summary>
        /// This class provides mechanisms for 
        /// serialization/deserialization of model objects to/from JSON
        /// </summary>
        public class Serializer
        {
            /// <summary>
            /// The options passed into the JSON serialization backend
            /// </summary>
            private JsonSerializerOptions serializerOptions = new JsonSerializerOptions()
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters =
                    {
                        new CardJsonConverter(),
                        new CommandJsonConverter()
                    }
            };

            /// <summary>
            /// Serializes a <see cref="Data.Desk"/> object to a file
            /// </summary>
            /// <param name="desk">Desk object to be serialized</param>
            /// <param name="jsonFilename">Target JSON file name</param>
            public void SerializeDeskToFile(Data.Desk desk, string jsonFilename)
            {
                var jsonString = JsonSerializer.Serialize(desk, serializerOptions);
                File.WriteAllText(jsonFilename, jsonString);
            }

            /// <summary>
            /// Deserializes a <see cref="Data.Desk"/> object from a file
            /// </summary>
            /// <param name="jsonFilename">Source JSON file name</param>
            /// <returns><see cref="Data.Desk"/> object decoded from the file contents</returns>
            public Data.Desk DeserializeDeskFromFile(string jsonFilename)
            {
                var jsonString = File.ReadAllText(jsonFilename);
                return JsonSerializer.Deserialize<Data.Desk>(jsonString, serializerOptions);
            }

            /// <summary>
            /// Serializes a <see cref="Data.Card"/> object to string
            /// </summary>
            /// <param name="card">Object to be serialized</param>
            /// <returns>String containing the serialized JSON</returns>
            public string SerializeCard(Data.Card card)
            {
                return JsonSerializer.Serialize(card, serializerOptions);
            }

            /// <summary>
            /// Deserializes a <see cref="Data.Card"/> object from string
            /// </summary>
            /// <param name="s">String containing the JSON encoded object</param>
            /// <returns>The deserialized <see cref="Data.Card"/></returns>
            public Data.Card DeserializeCard(string s)
            {
                return JsonSerializer.Deserialize<Data.Card>(s, serializerOptions);
            }

            /// <summary>
            /// Serializes a <see cref="Commands.Command"/> object to string
            /// </summary>
            /// <param name="command">Object to be serialized</param>
            /// <returns>String containing the serialized JSON</returns>
            public string SerializeCommand(Commands.Command command)
            {
                return JsonSerializer.Serialize(command, serializerOptions);
            }

            /// <summary>
            /// Deserializes a <see cref="Commands.Command"/> object from string
            /// </summary>
            /// <param name="s">String containing the JSON encoded object</param>
            /// <returns>The deserialized <see cref="Commands.Command"/></returns>
            public Commands.Command DeserializeCommand(string s)
            {
                return JsonSerializer.Deserialize<Commands.Command>(s, serializerOptions);
            }

        }

        /// <summary>
        /// <para>A <see cref="JsonConverter"/> class used for decoding command classes.</para>
        /// 
        /// <para>This class is necessary to distinguish between different <see cref="Commands.Command"/>
        /// subclasses based on the <c>type</c> field.</para>
        /// </summary>
        class CommandJsonConverter : JsonConverter<Commands.Command>
        {
            public override Commands.Command Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var secondReader = reader;

                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("A Command object should start here");
                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    reader.Read();                                      // Going into the next field
                    if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("A property should start here");
                    var propName = reader.GetString();                  // Reading the property name
                    reader.Read();                                      // Going to the property value
                    if (propName != "type") continue;                   // We are searching for the property called "type"

                    if (reader.TokenType != JsonTokenType.String) throw new JsonException("A \"type\" should be a string value");
                    var value = reader.GetString();

                    if (value == Commands.AddCardCommand.TYPE)
                    {
                        Commands.AddCardCommand res = JsonSerializer.Deserialize<Commands.AddCardCommand>(ref secondReader, options);
                        reader = secondReader;
                        return res;
                    }
                    else if (value == Commands.UploadImageCardCommand.TYPE)
                    {
                        Commands.UploadImageCardCommand res = JsonSerializer.Deserialize<Commands.UploadImageCardCommand>(ref secondReader, options);
                        reader = secondReader;
                        return res;
                    }
                    else throw new JsonException("A \"type\" should be either \""
                        + Commands.AddCardCommand.TYPE + "\" or \""
                        + Commands.UploadImageCardCommand.TYPE + "\"");
                }
                throw new JsonException("JSON parser can't find the \"type\" property");
            }

            public override void Write(Utf8JsonWriter writer, Commands.Command value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }

        /// <summary>
        /// <para>A <see cref="JsonConverter"/> class used for decoding card classes.</para>
        /// 
        /// <para>This class is necessary to distinguish between different <see cref="Data.Card"/>
        /// subclasses based on the <c>type</c> field.</para>
        /// </summary>
        class CardJsonConverter : JsonConverter<Data.Card>
        {
            public override Data.Card Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var secondReader = reader;

                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("A Command object should start here");
                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    reader.Read();                                      // Going into the next field
                    if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("A property should start here");
                    var propName = reader.GetString();                  // Reading the property name
                    reader.Read();                                      // Going to the property value
                    if (propName != "type") continue;                   // We are searching for the property called "type"

                    if (reader.TokenType != JsonTokenType.String) throw new JsonException("A \"type\" should be a string value");
                    var value = reader.GetString();

                    if (value == Data.TextCard.TYPE)
                    {
                        Data.TextCard res = JsonSerializer.Deserialize<Data.TextCard>(ref secondReader, options);
                        reader = secondReader;
                        return res;
                    }
                    else if (value == Data.ImageCard.TYPE)
                    {
                        Data.ImageCard res = JsonSerializer.Deserialize<Data.ImageCard>(ref secondReader, options);
                        reader = secondReader;
                        return res;
                    }
                    else throw new JsonException("A \"type\" should be either \"" + Data.TextCard.TYPE  + "\" or \"" + Data.ImageCard.TYPE + "\"");
                }
                throw new JsonException("JSON parser can't find the \"type\" property");
            }

            public override void Write(Utf8JsonWriter writer, Data.Card value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }
    }
}
