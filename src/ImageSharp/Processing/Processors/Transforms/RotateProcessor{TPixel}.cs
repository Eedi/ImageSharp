// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Advanced.ParallelUtils;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides methods that allow the rotating of images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class RotateProcessor<TPixel> : AffineTransformProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RotateProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="RotateProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public RotateProcessor(Configuration configuration, RotateProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, definition, source, sourceRectangle)
        {
            this.Degrees = definition.Degrees;
        }

        private float Degrees { get; }

        /// <inheritdoc/>
        protected override void AfterImageApply(Image<TPixel> destination)
        {
            ExifProfile profile = destination.Metadata.ExifProfile;
            if (profile is null)
            {
                return;
            }

            if (MathF.Abs(WrapDegrees(this.Degrees)) < Constants.Epsilon)
            {
                // No need to do anything so return.
                return;
            }

            profile.RemoveValue(ExifTag.Orientation);

            base.AfterImageApply(destination);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        {
            if (this.OptimizedApply(source, destination, this.Configuration))
            {
                return;
            }

            base.OnFrameApply(source, destination);
        }

        /// <summary>
        /// Wraps a given angle in degrees so that it falls withing the 0-360 degree range
        /// </summary>
        /// <param name="degrees">The angle of rotation in degrees.</param>
        /// <returns>The <see cref="float"/>.</returns>
        private static float WrapDegrees(float degrees)
        {
            degrees %= 360;

            while (degrees < 0)
            {
                degrees += 360;
            }

            return degrees;
        }

        /// <summary>
        /// Rotates the images with an optimized method when the angle is 90, 180 or 270 degrees.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="destination">The destination image.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>
        /// The <see cref="bool" />
        /// </returns>
        private bool OptimizedApply(
            ImageFrame<TPixel> source,
            ImageFrame<TPixel> destination,
            Configuration configuration)
        {
            // Wrap the degrees to keep within 0-360 so we can apply optimizations when possible.
            float degrees = WrapDegrees(this.Degrees);

            if (MathF.Abs(degrees) < Constants.Epsilon)
            {
                // The destination will be blank here so copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return true;
            }

            if (MathF.Abs(degrees - 90) < Constants.Epsilon)
            {
                this.Rotate90(source, destination, configuration);
                return true;
            }

            if (MathF.Abs(degrees - 180) < Constants.Epsilon)
            {
                this.Rotate180(source, destination, configuration);
                return true;
            }

            if (MathF.Abs(degrees - 270) < Constants.Epsilon)
            {
                this.Rotate270(source, destination, configuration);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Rotates the image 180 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="destination">The destination image.</param>
        /// <param name="configuration">The configuration.</param>
        private void Rotate180(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;

            ParallelHelper.IterateRows(
                source.Bounds(),
                configuration,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                            Span<TPixel> targetRow = destination.GetPixelRowSpan(height - y - 1);

                            for (int x = 0; x < width; x++)
                            {
                                targetRow[width - x - 1] = sourceRow[x];
                            }
                        }
                    });
        }

        /// <summary>
        /// Rotates the image 270 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="destination">The destination image.</param>
        /// <param name="configuration">The configuration.</param>
        private void Rotate270(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;
            Rectangle destinationBounds = destination.Bounds();

            ParallelHelper.IterateRows(
                source.Bounds(),
                configuration,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                            for (int x = 0; x < width; x++)
                            {
                                int newX = height - y - 1;
                                newX = height - newX - 1;
                                int newY = width - x - 1;

                                if (destinationBounds.Contains(newX, newY))
                                {
                                    destination[newX, newY] = sourceRow[x];
                                }
                            }
                        }
                    });
        }

        /// <summary>
        /// Rotates the image 90 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="destination">The destination image.</param>
        /// <param name="configuration">The configuration.</param>
        private void Rotate90(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;
            Rectangle destinationBounds = destination.Bounds();

            ParallelHelper.IterateRows(
                source.Bounds(),
                configuration,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                            int newX = height - y - 1;
                            for (int x = 0; x < width; x++)
                            {
                                if (destinationBounds.Contains(newX, x))
                                {
                                    destination[newX, x] = sourceRow[x];
                                }
                            }
                        }
                    });
        }
    }
}
