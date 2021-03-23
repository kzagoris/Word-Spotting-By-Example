namespace DoLFLibrary
{
    internal class Blob
    {
        private byte[] ImageData;
        // blobs image
        public byte[] Owner { get; set; }
        // image containing the blob
        public bool[,] BlobArray { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Right { get; set; }

        public int Bottom { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public float CenterOfGravityX { get; set; }

        public float CenterOfGravityY { get; set; }

        public int Area { get; set; }



        // Constructor

        public Blob(int X, int Y, int Width, int Height)
        {

            this.Width = Width;
            this.Height = Height;
            this.X = X;
            this.Y = Y;
            this.Right = this.X + this.Width - 1;
            this.Bottom = this.Y + this.Height - 1;
        }

        public Blob()
        {
        }

        public Blob(byte[] Image, int X, int Y, int Width, int Height)
            : this(X, Y, Width, Height)
        {
            this.ImageData = Image;
        }

        public Blob(byte[] Image, byte[] Owner, int X, int Y, int Width, int Height)
            : this(X, Y, Width, Height)
        {
            this.ImageData = Image;
            this.Owner = Owner;
        }

        public Blob(bool[,] BlobArray, int X, int Y, int Width, int Height)
            : this(X, Y, Width, Height)
        {
            this.BlobArray = BlobArray;
        }



    }

    internal class BlobCounter
    {
        private int ObjectsCount;
        private int[] ObjectLabels;
        

        // ProcessImage is only builds object labels map and count objects
        public void ProcessImage(byte[] src1, int width, int height)
        {
           

            // allocate labels array
            ObjectLabels = new int[width * height];
            // initial labels count
            int labelsCount = 0;

            // create map
            int maxObjects = ((width / 2) + 1) * ((height / 2) + 1) + 1;
            int[] map = new int[maxObjects];

            // initially map all labels to themself
            for (int i = 0; i < maxObjects; i++)
            {
                map[i] = i;
            }
            int p = 0, src = 0;
            int srcStride = width;

            // 1 - for pixels of the first row
            if (src1[src] != 0)
            {
                ObjectLabels[p] = ++labelsCount;
            }
            src++;
            ++p;

            for (int x = 1; x < width; x++, src++, p++)
            {
                // check if we need to label current pixel
                if (src1[src] != 0)
                {
                    // check if the previous pixel already labeled
                    if (src1[src - 1] != 0)
                    {
                        // label current pixel, as the previous
                        ObjectLabels[p] = ObjectLabels[p - 1];
                    }
                    else
                    {
                        // create new label
                        ObjectLabels[p] = ++labelsCount;
                    }
                }
            }

            // 2 - for other rows
            // for each row
            for (int y = 1; y < height; y++)
            {
                // for the first pixel of the row, we need to check
                // only upper and upper-right pixels
                if (src1[src] != 0)
                {
                    // check surrounding pixels
                    if (src1[src - srcStride] != 0)
                    {
                        // label current pixel, as the above
                        ObjectLabels[p] = ObjectLabels[p - width];
                    }
                    else if (src1[src + 1 - srcStride] != 0)
                    {
                        // label current pixel, as the above right
                        ObjectLabels[p] = ObjectLabels[p + 1 - width];
                    }
                    else
                    {
                        // create new label
                        ObjectLabels[p] = ++labelsCount;
                    }
                }
                src += 1;
                ++p;

                // check left pixel and three upper pixels
                for (int x = 1; x < width - 1; x++, src += 1, p++)
                {
                    if (src1[src] != 0)
                    {
                        // check surrounding pixels
                        if (src1[src - 1] != 0)
                        {
                            // label current pixel, as the left
                            ObjectLabels[p] = ObjectLabels[p - 1];
                        }
                        else if (src1[src - 1 - srcStride] != 0)
                        {
                            // label current pixel, as the above left
                            ObjectLabels[p] = ObjectLabels[p - 1 - width];
                        }
                        else if (src1[src - srcStride] != 0)
                        {
                            // label current pixel, as the above
                            ObjectLabels[p] = ObjectLabels[p - width];
                        }

                        if (src1[src + 1 - srcStride] != 0)
                        {
                            if (ObjectLabels[p] == 0)
                            {
                                // label current pixel, as the above right
                                ObjectLabels[p] = ObjectLabels[p + 1 - width];
                            }
                            else
                            {
                                int l1 = ObjectLabels[p];
                                int l2 = ObjectLabels[p + 1 - width];

                                if ((l1 != l2) && (map[l1] != map[l2]))
                                {
                                    // merge
                                    if (map[l1] == l1)
                                    {
                                        // map left value to the right
                                        map[l1] = map[l2];
                                    }
                                    else if (map[l2] == l2)
                                    {
                                        // map right value to the left
                                        map[l2] = map[l1];
                                    }
                                    else
                                    {
                                        // both values already mapped
                                        map[map[l1]] = map[l2];
                                        map[l1] = map[l2];
                                    }

                                    // reindex
                                    for (int i = 1; i <= labelsCount; i++)
                                    {
                                        if (map[i] != i)
                                        {
                                            // reindex
                                            int j = map[i];
                                            while (j != map[j])
                                            {
                                                j = map[j];
                                            }
                                            map[i] = j;
                                        }
                                    }
                                }
                            }
                        }

                        if (ObjectLabels[p] == 0)
                        {
                            // create new label
                            ObjectLabels[p] = ++labelsCount;
                        }
                    }
                }

                // for the last pixel of the row, we need to check
                // only upper and upper-left pixels
                if (src1[src] != 0)
                {
                    // check surrounding pixels
                    if (src1[src - 1] != 0)
                    {
                        // label current pixel, as the left
                        ObjectLabels[p] = ObjectLabels[p - 1];
                    }
                    else if (src1[src - 1 - srcStride] != 0)
                    {
                        // label current pixel, as the above left
                        ObjectLabels[p] = ObjectLabels[p - 1 - width];
                    }
                    else if (src1[src - srcStride] != 0)
                    {
                        // label current pixel, as the above
                        ObjectLabels[p] = ObjectLabels[p - width];
                    }
                    else
                    {
                        // create new label
                        ObjectLabels[p] = ++labelsCount;
                    }
                }
                src += 1;
                ++p;

            }


            // allocate remapping array
            int[] reMap = new int[map.Length];

            // count objects and prepare remapping array
            ObjectsCount = 0;
            for (int i = 1; i <= labelsCount; i++)
            {
                if (map[i] == i)
                {
                    // increase objects count
                    reMap[i] = ++ObjectsCount;
                }
            }
            // second pass to compete remapping
            for (int i = 1; i <= labelsCount; i++)
            {
                if (map[i] != i)
                {
                    reMap[i] = reMap[map[i]];
                }
            }

            // repair object labels
            for (int i = 0, n = ObjectLabels.Length; i < n; i++)
            {
                ObjectLabels[i] = reMap[ObjectLabels[i]];
            }
        }

        // Get array of objects rectangles
        public int[][] GetObjectRectangles(byte[] src, int width, int height)
        {

            // process the image
            ProcessImage(src, width, height);

            int[] labels = ObjectLabels;
            int count = ObjectsCount;

            int i = 0, label;

            // create object coordinates arrays
            int[] x1 = new int[count + 1];
            int[] y1 = new int[count + 1];
            int[] x2 = new int[count + 1];
            int[] y2 = new int[count + 1];

            for (int j = 1; j <= count; j++)
            {
                x1[j] = width;
                y1[j] = height;
            }

            // walk through labels array
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++, i++)
                {
                    // get current label
                    label = labels[i];

                    // skip unlabeled pixels
                    if (label == 0)
                        continue;

                    // check and update all coordinates

                    if (x < x1[label])
                    {
                        x1[label] = x;
                    }
                    if (x > x2[label])
                    {
                        x2[label] = x;
                    }
                    if (y < y1[label])
                    {
                        y1[label] = y;
                    }
                    if (y > y2[label])
                    {
                        y2[label] = y;
                    }
                }
            }

            // create rectangles
            var rects = new int[count][];

            for (int j = 1; j <= count; j++)
            {
                rects[j - 1] = new [] { x1[j], y1[j], x2[j] - x1[j] + 1, y2[j] - y1[j] + 1 };
            }

            return rects;
        }



        internal Blob[] GetObjectsWithoutArray(ZagImage<byte> SourceImage)
        {
            byte[] src = SourceImage.Data;
            int width = SourceImage.Width;
            int height = SourceImage.Height;
            // process the image
            ProcessImage(src, width, height);

            int[] labels = ObjectLabels;
            int count = ObjectsCount;

            int i = 0, label;

            // --- STEP 1 - find each objects coordinates

            // create object coordinates arrays
            int[] x1 = new int[count + 1];
            int[] y1 = new int[count + 1];
            int[] x2 = new int[count + 1];
            int[] y2 = new int[count + 1];

            //For Area
            int[] area = new int[count + 1];

            //For Center of Gravity
            long[] xc = new long[count + 1];
            long[] yc = new long[count + 1];

            for (int k = 1; k <= count; k++)
            {
                x1[k] = width;
                y1[k] = height;
            }

            // walk through labels array
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++, i++)
                {
                    // get current label
                    label = labels[i];

                    // skip unlabeled pixels
                    if (label == 0)
                        continue;

                    // check and update all coordinates

                    if (x < x1[label])
                    {
                        x1[label] = x;
                    }
                    if (x > x2[label])
                    {
                        x2[label] = x;
                    }
                    if (y < y1[label])
                    {
                        y1[label] = y;
                    }
                    if (y > y2[label])
                    {
                        y2[label] = y;
                    }

                    area[label]++;
                    xc[label] += x;
                    yc[label] += y;
                }
            }

            // --- STEP 2 - get each object
            Blob[] objects = new Blob[count];

            // create each array
            for (int k = 1; k <= count; k++)
            {
                int xmin = x1[k];
                int xmax = x2[k];
                int ymin = y1[k];
                int ymax = y2[k];
                int objectWidth = xmax - xmin + 1;
                int objectHeight = ymax - ymin + 1;


                objects[k - 1] = new Blob(xmin, ymin, objectWidth, objectHeight);
                objects[k - 1].CenterOfGravityX = xc[k] / (float)area[k];
                objects[k - 1].CenterOfGravityY = yc[k] / (float)area[k];
                objects[k - 1].Area = area[k];
            }

            return objects;
        }

        // Get array of objects images
        internal Blob[] GetObjectsWithArray(ZagImage<byte> SourceImage)
        {
            byte[] src = SourceImage.Data;
            int width = SourceImage.Width;
            int height = SourceImage.Height;


            // process the image
            ProcessImage(src, width, height);

            int[] labels = ObjectLabels;
            int count = ObjectsCount;

            // image size
            int i = 0, label;

            // --- STEP 1 - find each objects coordinates

            // create object coordinates arrays
            int[] x1 = new int[count + 1];
            int[] y1 = new int[count + 1];
            int[] x2 = new int[count + 1];
            int[] y2 = new int[count + 1];

            for (int k = 1; k <= count; k++)
            {
                x1[k] = width;
                y1[k] = height;
            }

            // walk through labels array
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++, i++)
                {
                    // get current label
                    label = labels[i];

                    // skip unlabeled pixels
                    if (label == 0)
                        continue;

                    // check and update all coordinates

                    if (x < x1[label])
                    {
                        x1[label] = x;
                    }
                    if (x > x2[label])
                    {
                        x2[label] = x;
                    }
                    if (y < y1[label])
                    {
                        y1[label] = y;
                    }
                    if (y > y2[label])
                    {
                        y2[label] = y;
                    }
                }
            }

            // --- STEP 2 - get each object
            Blob[] objects = new Blob[count];




            
            // create each array
            for (int k = 1; k <= count; k++)
            {
                int xmin = x1[k];
                int xmax = x2[k];
                int ymin = y1[k];
                int ymax = y2[k];
                int objectWidth = xmax - xmin + 1;
                int objectHeight = ymax - ymin + 1;

                bool[,] array = new bool[objectWidth, objectHeight];
                int p = ymin * width + xmin;

                int labelsOffset = width - objectWidth;

                // for each line
                for (int y = ymin; y <= ymax; y++)
                {
                    // copy each pixel
                    for (int x = xmin; x <= xmax; x++, p++)
                    {
                        if (labels[p] == k)
                        {
                            array[x - xmin, y - ymin] = true;
                        }
                        else
                        {
                            array[x - xmin, y - ymin] = false;
                        }
                    }
                    p += labelsOffset;
                }


                objects[k - 1] = new Blob(array, xmin, ymin, objectWidth, objectHeight);
            }

            return objects;
        }



        // Get array of objects images
        internal Blob[] GetObjects(byte[] src, int width, int height)
        {


            // process the image
            ProcessImage(src, width, height);

            int[] labels = ObjectLabels;
            int count = ObjectsCount;

            // image size
            int i = 0, label;

            // --- STEP 1 - find each objects coordinates

            // create object coordinates arrays
            int[] x1 = new int[count + 1];
            int[] y1 = new int[count + 1];
            int[] x2 = new int[count + 1];
            int[] y2 = new int[count + 1];

            for (int k = 1; k <= count; k++)
            {
                x1[k] = width;
                y1[k] = height;
            }

            // walk through labels array
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++, i++)
                {
                    // get current label
                    label = labels[i];

                    // skip unlabeled pixels
                    if (label == 0)
                        continue;

                    // check and update all coordinates

                    if (x < x1[label])
                    {
                        x1[label] = x;
                    }
                    if (x > x2[label])
                    {
                        x2[label] = x;
                    }
                    if (y < y1[label])
                    {
                        y1[label] = y;
                    }
                    if (y > y2[label])
                    {
                        y2[label] = y;
                    }
                }
            }

            // --- STEP 2 - get each object
            Blob[] objects = new Blob[count];


            int srcStride = width;


            // create each image
            for (int k = 1; k <= count; k++)
            {
                int xmin = x1[k];
                int xmax = x2[k];
                int ymin = y1[k];
                int ymax = y2[k];
                int objectWidth = xmax - xmin + 1;
                int objectHeight = ymax - ymin + 1;
                int dstStride = objectWidth;
                byte[] dst = new byte[dstStride * objectHeight];

                int p = ymin * width + xmin;

                int labelsOffset = width - objectWidth;

                // for each line
                for (int y = ymin; y <= ymax; y++)
                {
                    // copy each pixel
                    for (int x = xmin; x <= xmax; x++, p++)
                    {
                        if (labels[p] == k)
                        {
                            dst[x + y * dstStride] = src[(x + xmin) + (ymin + y) * srcStride];
                            
                        }
                    }
                    p += labelsOffset;
                }
                // unlock destination image

                objects[k - 1] = new Blob(dst, src, xmin, ymin, objectWidth, objectHeight);
            }


            return objects;
        }
    }

}
