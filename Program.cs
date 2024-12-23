using System.Globalization;

namespace SomeNameSpace
{
    public static class VideoComparer
    {
        public static void CompareVideos(string yuvFilePath, string tsFilePath, int width, int height, double frameRate)
        {
            List<double> frameDiffs = new List<double>();
            var firstYUVFrame = ReadYUVFrame(yuvFilePath, width, height, 1);

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = 1; i <= 511; i++)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(Convert.ToInt32(stopwatch.Elapsed.TotalSeconds));
                Console.Write(timeSpan.ToString("c"));
                Console.Write('\r');
                
                var tsFrame = DecodeTsFrame(tsFilePath, width, height, frameRate, i);

                if (firstYUVFrame.Length != tsFrame.Length)
                {
                    tsFrame = ResizeFrame(tsFrame, width, height);
                }

                double difference = CalculateFrameDifference(firstYUVFrame, tsFrame);
                frameDiffs.Add(difference);
            }
            Console.WriteLine("\n");
            Console.WriteLine($"Frame offset to sync: {frameDiffs.IndexOf(frameDiffs.Min())}");
        }

        private static byte[] ReadYUVFrame(string filePath, int frameWidth, int frameHeight, int frameNumber)
        {
            int ySize = frameWidth * frameHeight;                    // Y plane size
            int uvSize = (frameWidth / 2) * (frameHeight / 2);       // U or V plane size
            int frameSize = ySize + 2 * uvSize;
            byte[] frameData = new byte[frameSize];

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                fs.Seek(frameNumber * frameSize, SeekOrigin.Begin);
                int bytesRead = 0;
                while (bytesRead < frameSize)
                {
                    int read = fs.Read(frameData, bytesRead, frameSize - bytesRead);
                    if (read == 0)
                    {
                        throw new EndOfStreamException("Unexpected end of file while reading YUV frame.");
                    }
                    bytesRead += read;
                }
            }

            return frameData;
        }

        private static byte[] DecodeTsFrame(string tsFilePath, int width, int height, double frameRate, int frameIndex = 0)
        {
            string ffmpegPath = @"D:\ffmpeg\bin\ffmpeg.exe"; // Path to the ffmpeg executable

            // Calculate the expected frame size (assuming YUV420p format)
            int ySize = width * height;
            int uvSize = (width / 2) * (height / 2);
            int frameSize = ySize + 2 * uvSize;

            // OLd argument with bad performance
            //string arguments = $"-i \"{tsFilePath}\" -vf \"select=eq(n\\,{frameIndex})\" -vsync vfr -f rawvideo -";

            double timestamp = frameIndex / frameRate;
            string arguments = $" -i \"{tsFilePath}\" -ss {timestamp.ToString(CultureInfo.InvariantCulture)} -frames:v 1 -f rawvideo -";

            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (System.Diagnostics.Process process = new System.Diagnostics.Process { StartInfo = startInfo })
            {
                process.Start();

                using (MemoryStream ms = new MemoryStream())
                {
                    // Redirect the standard output stream of FFmpeg to the MemoryStream
                    process.StandardOutput.BaseStream.CopyTo(ms);

                    // Ensure FFmpeg completes and check for errors
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new InvalidOperationException($"FFmpeg failed: {error}");
                    }

                    // Ensure the frame size matches the expected size
                    byte[] frameData = ms.ToArray();
                    if (frameData.Length != frameSize)
                    {
                        throw new InvalidOperationException($"Extracted frame size ({frameData.Length} bytes) does not match expected size ({frameSize} bytes).");
                    }

                    return frameData;
                }
            }
        }

        private static byte[] ResizeFrame(byte[] frame, int width, int height)
        {

            int expectedSize = (width * height * 3) / 2;
            if (frame.Length > expectedSize)
            {
                // Truncate if the frame is larger
                return frame.Take(expectedSize).ToArray();
            }
            else if (frame.Length < expectedSize)
            {
                // Pad with zeros if the frame is smaller
                return frame.Concat(new byte[expectedSize - frame.Length]).ToArray();
            }

            return frame;
        }

        private static double CalculateFrameDifference(byte[] frame1, byte[] frame2)
        {
            if (frame1.Length != frame2.Length)
            {
                throw new ArgumentException("Frames must have the same size");
            }

            long totalDifference = 0;

            for (int i = 0; i < frame1.Length; i++)
            {
                totalDifference += Math.Abs(frame1[i] - frame2[i]);
            }

            return totalDifference;
            //return (double)totalDifference / frame1.Length;
        }
    }

    public class MyClass
    {
        public static void Main()
        {
            string yuvFilePath = @"D:\Raw.yuv";
            string tsFilePath = @"D:\video.ts";
            int width = 1920; 
            int height = 1080; 
            double frameRate = 25; 
            VideoComparer.CompareVideos(yuvFilePath, tsFilePath, width, height, frameRate);
            Console.ReadKey();
        }
    }
}
