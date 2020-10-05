using Microsoft.Data.Sqlite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace CompletelyUnsafeMessenger
{
    namespace Controller
    {
        public class Images
        {
            private string imagesRoot;

            private string imagesCacheRoot;

            /// <summary>
            /// A lock used to synchronize file writing process
            /// </summary>
            private readonly ReaderWriterLock fileReaderWriterLock = new ReaderWriterLock();

            private string GetScreenImageFilePath(string name)
            {
                var screenName = Path.GetFileNameWithoutExtension(name) + ".screen.jpeg";
                return Path.Combine(imagesCacheRoot, screenName);
            }
            private string GetThumbImageFilePath(string name)
            {
                var screenName = Path.GetFileNameWithoutExtension(name) + ".thumbnail.jpeg";
                return Path.Combine(imagesCacheRoot, screenName);
            }

            public Images(string imagesRoot, string imagesCacheRoot)
            {
                this.imagesRoot = imagesRoot;
                this.imagesCacheRoot = imagesCacheRoot;
            }

            /// <summary>
            /// Checks if a file with the same name (excluding extension) is already existing in 
            /// the data folder
            /// </summary>
            /// <param name="nameWithoutExtension">The name without extension to be checked</param>
            /// <returns><c>true</c> if the file exists</returns>
            private bool CheckNameExists(string nameWithoutExtension)
            {
                string[] images;
                try
                {
                    fileReaderWriterLock.AcquireReaderLock(Timeout.Infinite);
                    images = Directory.GetFiles(imagesRoot);
                } 
                finally
                {
                    fileReaderWriterLock.ReleaseReaderLock();
                }

                for (int i = 0; i < images.Length; i++)
                {
                    if (Path.GetFileNameWithoutExtension(images[i]) == nameWithoutExtension)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// This function updates the cache for a single image
            /// </summary>
            /// <param name="name">Name of the image</param>
            /// <param name="imageInputStream">The input stream from which the image can be read</param>
            private void UpdateImageCache(string name, Stream imageInputStream)
            {
                Logger.Log("Building cache for " + name);

                // Making the "screen" version. 

                // This version will be used to show on the screen
                // Here we assume thet "greater than FullHD" is enough,
                // but we never reduce size more than twice by each coordinate.
                using Image image = Image.Load(imageInputStream);

                // We are checking if the image will 
                // still be bigger than FullHD after 
                // resizing by 2.

                int diameter = Math.Max(image.Width, image.Height);
                int fullHDDiam = 1920;

                int screenSizeWidth, screenSizeHeight;
                if (diameter / 2 >= fullHDDiam)
                {
                    // If a half of the diameter is still more than FullHD...
                    screenSizeWidth = image.Width / 2;
                    screenSizeHeight = image.Height / 2;
                }
                else if (diameter * 3 / 4 >= fullHDDiam)
                {
                    // If three quarters of the diameter is still more than FullHD...
                    screenSizeWidth = image.Width * 3 / 4;
                    screenSizeHeight = image.Height * 3 / 4;
                }
                else
                {
                    // Else leave the size as is
                    screenSizeWidth = image.Width;
                    screenSizeHeight = image.Height;
                }

                // Making the "screen" size
                var screenImage = image.Clone(x => x.Resize(screenSizeWidth, screenSizeHeight));
                try
                {
                    fileReaderWriterLock.AcquireWriterLock(Timeout.Infinite);
                    var screenName = GetScreenImageFilePath(name);

                    // Saving with moderate quality
                    screenImage.SaveAsJpeg(screenName, new JpegEncoder { Quality = 85 });
                }
                finally
                {
                    fileReaderWriterLock.ReleaseWriterLock();
                }

                // Generating thumbnail image
                int reducedDiam = diameter;
                int thumbnailDiameter = 1200; // 600 pixels * 2 (for possible user "retina" screen)

                // Calculating a divider 1, 2/3, 1/2, 2/5... that will make the image smaller than 1200
                int divider = 2;
                while (diameter * 2 / divider > thumbnailDiameter)
                {
                    divider++;
                }
                divider--;  // The image should be >= 1200

                // Making the "thumbnail" size
                var thumbWidth = image.Width * 2 / divider;
                var thumbHeight = image.Height * 2 / divider;
                var thumbImage = image.Clone(x => x.Resize(thumbWidth, thumbHeight));
                try
                {
                    fileReaderWriterLock.AcquireWriterLock(Timeout.Infinite);
                    var thumbName = GetThumbImageFilePath(name);

                    // Saving with low quality
                    screenImage.SaveAsJpeg(thumbName, new JpegEncoder { Quality = 50 });
                }
                finally
                {
                    fileReaderWriterLock.ReleaseWriterLock();
                }
            }

            /// <summary>
            /// Rebuilds the caches for all the images found in <c>imagesRoot</c> folder 
            /// (specified in the constructor).
            /// </summary>
            public void RebuildAllCaches()
            {
                lock (this)
                {
                    string[] images;
                    try
                    {
                        fileReaderWriterLock.AcquireReaderLock(Timeout.Infinite);
                        images = Directory.GetFiles(imagesRoot);
                    }
                    finally
                    {
                        fileReaderWriterLock.ReleaseReaderLock();
                    }

                    for (int i = 0; i < images.Length; i++)
                    {
                        try
                        {
                            fileReaderWriterLock.AcquireWriterLock(Timeout.Infinite);
                            using var fs = new FileStream(images[i], FileMode.Open);
                            UpdateImageCache(Path.GetFileName(images[i]), fs);
                        }
                        finally
                        {
                            fileReaderWriterLock.ReleaseWriterLock();
                        }
                    }
                }
            }

            /// <summary>
            /// <para>Adds an image to the data folder and to the cache. There are 3 different 
            /// quality/resolution configurations being saved:</para>
            /// <list type="number">
            /// <item>Original. The original image is copied as is to the data folder.</item>
            /// <item>Screen. The original image is shrinked to be shown on an average display 
            /// and reencoded as JPEG with moderate quality.</item>
            /// <item>Thumbnail. The original image is reduced in size to ~1200 pixels in diameter
            /// and reencoded as JPEG with low quality</item>
            /// </list>
            /// </summary>
            /// <param name="name">The expected image file name</param>
            /// <param name="imageInputStream">The image data stream to read from</param>
            /// <returns>The name actually used to save the image. If no name collision occured, the new
            /// image will be saved under the name provided in the <paramref name="name"/> parameter.
            /// Otherwise, the function will slightly modify the name and return the modified version
            /// to the caller.</returns>
            public string Add(string name, Stream imageInputStream)
            {
                lock (this)
                {
                    var ext = Path.GetExtension(name);

                    // Checking for collisions and generating the new name if found
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(name);
                    if (CheckNameExists(nameWithoutExt))
                    {
                        int suffix = 1;
                        while (CheckNameExists(nameWithoutExt + "." + suffix)) { suffix++; }
                        name = nameWithoutExt + "." + suffix + ext;
                    }

                    // Decoding the image and saving it in different qualities and resolutions
                    if (ext.Length > 0 && ext[0] == '.') ext = ext.Substring(1);
                    if (Apache.MimeTypes.ContainsKey(ext))
                    {
                        string mimeType = Apache.MimeTypes[ext];

                        string[] supportedMimeTypes =
                        {
                            "image/jpeg", "image/png", "image/tiff", "image/bmp"
                        };

                        if (supportedMimeTypes.Contains(mimeType))
                        {
                            // 1. Saving the original image
                            try
                            {
                                fileReaderWriterLock.AcquireWriterLock(Timeout.Infinite);
                                var origPath = Path.Combine(imagesRoot, name);
                                using (FileStream origImgTarget = new FileStream(origPath, FileMode.Create))
                                {
                                    imageInputStream.CopyTo(origImgTarget);
                                }
                            }
                            finally
                            {
                                fileReaderWriterLock.ReleaseWriterLock();
                            }

                            // Creating image caches for the new image
                            UpdateImageCache(name, imageInputStream);
                        }
                        else
                        {
                            throw new Exception("Unsupported image format: " + mimeType);
                        }
                    }
                    else
                    {
                        throw new Exception("Unknown image format: " + ext);
                    }

                    return name;
                }
            }

            /// <summary>
            /// Opens the original image from the data folder and processes an action on it
            /// </summary>
            /// <param name="name">Image name</param>
            /// <param name="act">The action to be processed</param>
            public void UsingOriginal(string name, Action<Stream> act)
            {
                try
                {
                    fileReaderWriterLock.AcquireReaderLock(Timeout.Infinite);
                    using var fs = new FileStream(Path.Combine(imagesRoot, name), FileMode.Open);
                    act(fs);
                }
                finally
                {
                    fileReaderWriterLock.ReleaseReaderLock();
                }
            }

            /// <summary>
            /// Opens the screen image from the cache and processes an action on it
            /// </summary>
            /// <param name="name">Image name</param>
            /// <param name="act">The action to be processed</param>
            public void UsingScreen(string name, Action<Stream> act)
            {
                try
                {
                    fileReaderWriterLock.AcquireReaderLock(Timeout.Infinite);
                    using var fs = new FileStream(GetScreenImageFilePath(name), FileMode.Open);
                    act(fs);
                }
                finally
                {
                    fileReaderWriterLock.ReleaseReaderLock();
                }
            }

            /// <summary>
            /// Opens the thumbnail image from the cache and processes an action on it
            /// </summary>
            /// <param name="name">Image name</param>
            /// <param name="act">The action to be processed</param>
            public void UsingThumbnail(string name, Action<Stream> act)
            {
                try
                {
                    fileReaderWriterLock.AcquireReaderLock(Timeout.Infinite);
                    using var fs = new FileStream(GetThumbImageFilePath(name), FileMode.Open);
                    act(fs);
                }
                finally
                {
                    fileReaderWriterLock.ReleaseReaderLock();
                }
            }

        }
    }
}
