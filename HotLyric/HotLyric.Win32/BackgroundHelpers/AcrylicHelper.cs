﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.System;
using System.Threading;
using System.Windows.Resources;
using System.Windows;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.Graphics.Canvas.Effects;
using Windows.UI;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Numerics;
using Windows.Graphics.Imaging;
using Color = Windows.UI.Color;

namespace HotLyric.Win32.BackgroundHelpers
{
    public class AcrylicHelper : IDisposable, INotifyPropertyChanged
    {
        #region Default Values

        public static readonly Color DefaultTintColor = Color.FromArgb(255, 249, 249, 249);
        public static readonly double DefaultTintOpacity = 0;
        public static readonly bool DefaultIsShadowVisible = true;
        public static readonly double? DefaultTintLuminosityOpacity = 0.85;
        public static readonly Color DefaultFallbackColor = Color.FromArgb(255, 252, 252, 252);
        public static readonly bool DefaultAlwaysUseFallback = false;

        #endregion Default Values

        private const string NoiseAsset = "NoiseAsset_256X256_PNG.png";
        private static CompositionSurfaceFactory? compositionSurfaceFactory;
        private static SemaphoreSlim asyncLock = new SemaphoreSlim(1, 1);

        private const double defaultTintOpacity = 1.0;
        private const float defaultBlurAmount = 30f;

        private static readonly TimeSpan defaultSwitchDuration = TimeSpan.FromMilliseconds(167);


        private CompositionEffectBrush brush;

        private double tintOpacity = DefaultTintOpacity;
        private double? tintLuminosityOpacity = DefaultTintLuminosityOpacity;
        private Color tintColor = DefaultTintColor;
        private Color fallbackColor = DefaultFallbackColor;
        private bool useFallback = DefaultAlwaysUseFallback;
        private double noiseScaleRatio = 1d;

        private CompositionEasingFunction? fallbackTransitionEasing;
        private CompositionScopedBatch? switchTransitionBatch;

        private CompositionSurfaceBrush noiseBrush;
        private bool disposedValue;

        private AcrylicHelper(CompositionSurfaceBrush noiseBrush)
        {
            this.noiseBrush = noiseBrush ?? throw new ArgumentNullException(nameof(noiseBrush));

            var effect = CreateAcrylicEffect();
            var compositor = CompositionThread.Instance.Compositor;
            var factory = compositor.CreateEffectFactory(effect, new[]
            {
                "CrossFadeEffect.CrossFade",
                "LuminosityColorEffect.Color",
                "TintColorEffect.Color",
                "FallbackColorEffect.Color",
                "TintColorEffectWithoutAlpha.Color",
                "TintColorOpacityEffect.Opacity",
                "TintColorWithoutAlphaOpacityEffect.Opacity"
            });

            var brush = factory.CreateBrush();

            var backdropSource = compositor.CreateBackdropBrush();
            brush.SetSourceParameter("source", backdropSource);
            brush.SetSourceParameter("noise", noiseBrush);

            this.brush = brush;
        }

        public Compositor Compositor => brush.Compositor;

        public CompositionBrush Brush => brush;

        public double TintOpacity
        {
            get => tintOpacity;
            set => SetProperty(ref tintOpacity, value);
        }

        public double? TintLuminosityOpacity
        {
            get => tintLuminosityOpacity;
            set => SetProperty(ref tintLuminosityOpacity, value);
        }

        public Color TintColor
        {
            get => tintColor;
            set => SetProperty(ref tintColor, value);
        }

        public Color FallbackColor
        {
            get => fallbackColor;
            set => SetProperty(ref fallbackColor, value);
        }

        public bool UseFallback
        {
            get => useFallback;
            set => SetProperty(ref useFallback, value);
        }

        public double NoiseScaleRatio
        {
            get => noiseScaleRatio;
            set
            {
                if (SetProperty(ref noiseScaleRatio, value))
                {
                    noiseScaleRatio = value;
                    noiseBrush.Scale = new Vector2((float)noiseScaleRatio);
                }
            }
        }

        private bool SetProperty<T>(ref T prop, T value, [CallerMemberName] string propName = "")
        {
            if (!object.Equals(prop, value))
            {
                prop = value;
                OnPropertyChanged(propName);
                return true;
            }

            return false;
        }

        private void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            switch (propName)
            {
                case nameof(TintOpacity):
                case nameof(TintLuminosityOpacity):
                case nameof(TintColor):
                    {
                        UpdateTintState();
                    }
                    break;

                case nameof(FallbackColor):
                    {
                        brush.Properties.InsertColor("FallbackColorEffect.Color", FallbackColor);
                    }
                    break;
                case nameof(UseFallback):
                    {
                        UpdateUseFallbackState();
                    }
                    break;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void UpdateTintState()
        {
            var luminosityColor = ColorConversion.GetEffectiveLuminosityColor(TintColor, TintOpacity, TintLuminosityOpacity);
            var tintColor = ColorConversion.GetEffectiveTintColor(TintColor, TintOpacity, TintLuminosityOpacity);

            brush.Properties.InsertColor("LuminosityColorEffect.Color", luminosityColor);
            brush.Properties.InsertColor("TintColorEffectWithoutAlpha.Color", tintColor);
            brush.Properties.InsertColor("TintColorEffect.Color", tintColor);


            brush.Properties.InsertScalar("TintColorWithoutAlphaOpacityEffect.Opacity", tintColor.A == 255 ? 1f : 0f);
            brush.Properties.InsertScalar("TintColorOpacityEffect.Opacity", tintColor.A == 255 ? 0f : 1f);
        }

        private void UpdateUseFallbackState()
        {
            CancelFallbackSwitchAnimation();

            if (fallbackTransitionEasing == null)
            {
                fallbackTransitionEasing = Compositor.CreateCubicBezierEasingFunction(new Vector2(0.5f, 0.0f), new Vector2(0.0f, 0.9f));
            }

            var fromValue = 0f;
            var toValue = 0f;

            if (UseFallback)
            {
                fromValue = 1f;
            }
            else
            {
                toValue = 1f;
            }

            var animation = Compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0.0f, fromValue);
            animation.InsertKeyFrame(1.0f, toValue, fallbackTransitionEasing);
            animation.Duration = defaultSwitchDuration;
            animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;

            var batch = Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += FallbackSwitchAnimation_Completed;
            brush.StartAnimation("CrossFadeEffect.CrossFade", animation);
            batch.End();

            switchTransitionBatch = batch;
        }

        private void FallbackSwitchAnimation_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            CancelFallbackSwitchAnimation(true);
            brush.Properties.InsertScalar("CrossFadeEffect.CrossFade", UseFallback ? 0f : 1f);
        }

        private void CancelFallbackSwitchAnimation(bool completed = false)
        {
            if (switchTransitionBatch != null)
            {
                if (!completed)
                {
                    switchTransitionBatch.Completed -= FallbackSwitchAnimation_Completed;
                }

                switchTransitionBatch.Dispose();
                switchTransitionBatch = null;
                brush.StopAnimation("CrossFadeEffect.CrossFade");
            }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)

                    if (switchTransitionBatch != null)
                    {
                        switchTransitionBatch.Completed -= FallbackSwitchAnimation_Completed;
                        switchTransitionBatch.Dispose();
                        switchTransitionBatch = null;
                        brush.StopAnimation("CrossFadeEffect.CrossFade");
                    }

                    brush?.Dispose();
                    brush = null!;

                    noiseBrush?.Dispose();
                    noiseBrush = null!;

                    fallbackTransitionEasing?.Dispose();
                    fallbackTransitionEasing = null;
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~AcrylicHelper()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public static async Task<AcrylicHelper> CreateAsync()
        {
            var noiseBrush = await CreateNoiseSurfaceBrushAsync();

            var helper = new AcrylicHelper(noiseBrush);

            return helper;
        }

        #region Create Brush

        private static ICanvasEffect CreateAcrylicEffect()
        {
            var acrylic = CreateAcrylicEffectCore();
            var fallback = new ColorSourceEffect()
            {
                Name = "FallbackColorEffect",
                Color = DefaultFallbackColor
            };

            var effect = new CrossFadeEffect()
            {
                Name = "CrossFadeEffect",
                Source1 = acrylic,
                Source2 = fallback,
                CrossFade = 1,
            };

            return effect;
        }

        private static ICanvasEffect CreateAcrylicEffectCore()
        {
            var luminosityColor = ColorConversion.GetEffectiveLuminosityColor(DefaultTintColor, defaultTintOpacity, null);
            var tintColor = ColorConversion.GetEffectiveTintColor(DefaultTintColor, defaultTintOpacity, null);

            var effect = new CompositeEffect()
            {
                Mode = CanvasComposite.SourceOver,
                Sources =
                {
                    new OpacityEffect()
                    {
                        Name = "TintColorOpacityEffect",
                        Opacity = tintColor.A == 255 ? 0f : 1f,
                        Source = new BlendEffect()
                        {
                            Mode = BlendEffectMode.Luminosity,
                            Foreground = new ColorSourceEffect()
                            {
                                Name = "TintColorEffect",
                                Color = tintColor
                            },
                            Background = new BlendEffect
                            {
                                Mode = BlendEffectMode.Color,
                                Foreground = new ColorSourceEffect
                                {
                                    Name = "LuminosityColorEffect",
                                    Color = luminosityColor,
                                },
                                Background = new GaussianBlurEffect()
                                {
                                    Name = "GaussianBlurEffect",
                                    BlurAmount = defaultBlurAmount,
                                    Source = new CompositionEffectSourceParameter("source"),
                                    BorderMode = EffectBorderMode.Hard
                                },
                            },

                        },
                    },
                    new OpacityEffect()
                    {
                        Name = "TintColorWithoutAlphaOpacityEffect",
                        Opacity = tintColor.A == 255 ? 1f : 0f,
                        Source = new ColorSourceEffect()
                        {
                            Name = "TintColorEffectWithoutAlpha",
                            Color = tintColor
                        }
                    },
                    new OpacityEffect()
                    {
                        Opacity = 0.02f,
                        Source = new BorderEffect()
                        {
                            Source = new CompositionEffectSourceParameter("noise"),
                            ExtendX = CanvasEdgeBehavior.Wrap,
                            ExtendY = CanvasEdgeBehavior.Wrap,
                        }
                    },
                }
            };

            return effect;
        }

        private static Task<CompositionSurfaceBrush> CreateNoiseSurfaceBrushAsync()
        {
            var tcs = new TaskCompletionSource<CompositionSurfaceBrush>();

            var queue = CompositionThread.Instance.DispatcherQueue.TryEnqueue(async () =>
            {
                if (compositionSurfaceFactory == null)
                {
                    try
                    {
                        await asyncLock.WaitAsync();

                        if (compositionSurfaceFactory == null)
                        {
                            var uri = new Uri($"Assets/{NoiseAsset}", UriKind.Relative);

                            var streamInfo = Application.GetResourceStream(uri);
                            
                            using (var memoryStream = new InMemoryRandomAccessStream())
                            {
                                using (var stream = streamInfo.Stream)
                                {
                                    await RandomAccessStream.CopyAsync(stream.AsInputStream(), memoryStream);
                                }

                                memoryStream.Seek(0);

                                byte[] imageData;
                                Windows.Foundation.Size imageSize;
                                BitmapSize imageBitmapSize;
                                float dpi;

                                using (var image = await CanvasBitmap.LoadAsync(DeviceHolder.CanvasDevice, memoryStream))
                                {
                                    imageData = image.GetPixelBytes();
                                    imageSize = image.Size;
                                    imageBitmapSize = image.SizeInPixels;
                                    dpi = image.Dpi;
                                }

                                compositionSurfaceFactory = new CompositionSurfaceFactory(imageSize);
                                compositionSurfaceFactory.Draw += (s, a) =>
                                {
                                    using (var image = CanvasBitmap.CreateFromBytes(a.DrawingSession, imageData, (int)imageBitmapSize.Width, (int)imageBitmapSize.Height, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, dpi, CanvasAlphaMode.Premultiplied))
                                    {
                                        a.DrawingSession.DrawImage(image);
                                    }
                                };
                            }
                        }
                    }
                    finally
                    {
                        asyncLock.Release();
                    }
                }

                var noiseSurfaceBrush = CompositionThread.Instance.Compositor.CreateSurfaceBrush(compositionSurfaceFactory.Surface);
                tcs.SetResult(noiseSurfaceBrush);
            });

            return tcs.Task;
        }


        #endregion Create Brush
    }
}
