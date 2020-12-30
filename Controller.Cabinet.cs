using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace CompletelyUnsafeMessenger
{
    namespace Controller
    {
        /// <summary>
        /// A cabinet controller
        /// This class controls the data in the "cabinet", 
        /// saves it and loads to/from the cabinet file
        /// </summary>
        public class Cabinet
        {
            private Model.Data.Cabinet data = new Model.Data.Cabinet();
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
            public IImmutableDictionary<string, Model.Data.Card> Cards { 
                get { 
                    return data.Cards.ToImmutableDictionary(); 
                }
            }

            /// <summary>
            /// Updates a card in the model cabinet storage
            /// </summary>
            /// <param name="id">A card id to update</param>
            /// <param name="card">The new card value</param>
            public void UpdateCard(string id, Model.Data.Card card)
            {
                if (data.Cards.ContainsKey(id))
                {
                    // Updating the card in the cabbinet
                    data.Cards[id] = card;
                } else
                {
                    // Adding the new card to the cabinet
                    data.Cards.Add(id, card);
                }

                // Saving the new desk contents
                Save();
            }

            ///<summary>
            /// Returns the full list of card IDs
            /// </summary>
            public ImmutableList<string> ListCardIDs()
            {
                return data.Cards.Keys.ToImmutableList<string>();
            }
        }
    }

}
