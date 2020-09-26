using System;
using System.Collections.Immutable;
using System.IO;

namespace CompletelyUnsafeMessenger
{
    namespace Controller
    {
        /// <summary>
        /// A desk controller
        /// This class controls the data on the desk, 
        /// saves it and loads to/from the desk file
        /// </summary>
        public class Desk
        {
            private Model.Data.Desk data = new Model.Data.Desk();
            private readonly Model.Serializer serializer = new Model.Serializer();
            private string jsonFilename = null;

            /// <summary>
            /// Loads the desk contents from disk
            /// </summary>
            /// <param name="jsonFilename">File on disk that should be used for saving/loading of the desk contents</param>
            public void Load(string jsonFilename)
            {
                this.jsonFilename = jsonFilename;
                data = serializer.DeserializeDeskFromFile(jsonFilename);
            }

            /// <summary>
            /// Saves the desk contents to disk (file name is specified in the constructor)
            /// </summary>
            /// <param name="backup">Make a backup of the existing file before saving</param>
            public void Save(bool backup = true)
            {
                if (jsonFilename == null) throw new Exception("Can not save the file. File name isn't specified");

                if (backup && File.Exists(jsonFilename))
                {
                    File.Copy(jsonFilename, jsonFilename + ".backup", true);
                }

                serializer.SerializeDeskToFile(data, jsonFilename);
            }

            /// <summary>
            /// Current cards on the desk
            /// </summary>
            public IImmutableList<Model.Data.Card> Cards { 
                get { 
                    return data.Cards.ToImmutableList(); 
                }
            }

            /// <summary>
            /// Adds a new message to the model and generating the 
            /// WebSockGram sequence for broadcasting it to the clients
            /// </summary>
            /// <param name="card">A card to add</param>
            public void AddCard(Model.Data.Card card)
            {
                // Adding the new card to the desk
                data.Cards.Add(card);

                // Saving the new desk contents
                Save();
            }
        }
    }

}
