using System;
using System.Collections.Generic;

namespace CompletelyUnsafeMessenger
{
    namespace Model
    {
        // These classes represent the possible card types
        namespace Data
        {
            /// <summary>
            /// Represents an abstract card
            /// </summary>
            public abstract class Card
            {
                public string Type { get; }
                protected Card(string type)
                {
                    this.Type = type;
                }
            }

            /// <summary>
            /// Represents a card with some text on it
            /// </summary>
            public class TextCard : Card
            {
                public static string TYPE = "text";
                public string Text { get; set; }
                
                public TextCard(string text) : base(TYPE)
                {
                    this.Text = text;
                }
                public TextCard() : base(TYPE) { }
            }

            /// <summary>
            /// Represents a card with an image. It holds only the image name and URL. 
            /// The image data itself should be kept in a separate file (accessible via the provided URL)
            /// </summary>
            public class ImageCard : Card
            {
                public static string TYPE = "image";
                public string Filename { get; set; }
                public Uri Link { get; set; }
                public ImageCard(string filename) : base(TYPE)
                {
                    this.Filename = filename;
                }
                public ImageCard(string filename, Uri link) : this(filename)
                {
                    this.Link = link;
                }
                public ImageCard() : base(TYPE) { }
            }

            /// <summary>
            /// Represents a desk with cards on it
            /// </summary>
            public class Desk
            {
                private List<Card> cards = new List<Card>();
                public ICollection<Card> Cards
                { 
                    get { return cards; }
                    set { cards = new List<Card>(value); } 
                }
                public Desk() { }
                public Desk(IEnumerable<Card> cards)
                {
                    this.cards = new List<Card>(cards);
                }
            }
        }
    }
}
