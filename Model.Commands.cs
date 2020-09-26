using System;
using System.Collections.Generic;
using System.Text;

namespace CompletelyUnsafeMessenger
{
    namespace Model
    {
        /// <summary>
        /// <para>This namespace holds models for the client-server API protocol commands.</para>
        ///
        /// <para>The general format of the protocol implies sending these objects being serialized 
        /// to JSON thru a WebSocket stream. Some commands include data binary blobs appended 
        /// to them. In this case one WebSocket message should contain the command JSON itself,
        /// and the next message should contain the binary data appended to it.</para>
        /// </summary>
        namespace Commands
        {
            /// <summary>
            /// Abstract command
            /// </summary>
            public abstract class Command
            {
                public string Type { get; }
                protected Command(string type)
                {
                    this.Type = type;
                }
            }

            /// <summary>
            /// Command that adds a card to the bottom of the table
            /// </summary>
            public class AddCardCommand : Command
            {
                public static string TYPE = "add_card";
                public Data.Card Card { get; set; }
                public AddCardCommand(Data.Card card) : base(TYPE)
                {
                    this.Card = card;
                }
                public AddCardCommand() : base(TYPE) { }
            }

            /// <summary>
            /// <para>Command that sends an image for appending a new image card</para>
            ///
            /// <para>This command can be issued from the client side only.
            /// This command's WebSocket message should be followed by a binary 
            /// message containing the image data itself.
            /// The Link field from an included message is ignored and will be filled by the server</para>
            /// </summary>
            public class UploadImageCardCommand : Command
            {
                public static string TYPE = "upload_image_card";
                public Data.ImageCard Card { get; set; }
                public UploadImageCardCommand(Data.ImageCard card) : base(TYPE)
                {
                    this.Card = card;
                }
                public UploadImageCardCommand() : base(TYPE) { }
            }
        }
    }
}
