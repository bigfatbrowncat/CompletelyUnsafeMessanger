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
            public class UpdateCardCommand : Command
            {
                public static string TYPE = "update_card";
                public string Id { get; set; }
                public Data.Card Value { get; set; }
                public UpdateCardCommand(string Id, Data.Card value) : base(TYPE)
                {
                    this.Id = Id;
                    this.Value = value;
                }
                public UpdateCardCommand() : base(TYPE) { }
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
                public string Id { get; set; }
                public Data.ImageCard Value { get; set; }
                public UploadImageCardCommand(Data.ImageCard value) : base(TYPE)
                {
                    this.Value = value;
                }
                public UploadImageCardCommand() : base(TYPE) { }
            }

            /// <summary>
            /// Command that requests the card IDs list
            /// </summary>
            public class ListCardIDsCommand : Command
            {
                public static string TYPE = "list_card_ids";

                private List<string> ids;
                public IList<string> Ids { 
                    get { return ids; } 
                    set { ids = new List<string>(value); } 
                }
                public ListCardIDsCommand(IList<string> Ids) : base(TYPE)
                {
                    this.Ids = Ids;
                }
                public ListCardIDsCommand() : base(TYPE)
                {
                }
            }
        }
    }
}
