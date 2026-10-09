using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// Shows a <see cref="PixelBuffer"/> through a point-filtered texture stretched over the
    /// element. Lay it out at an integer multiple of the buffer size for even pixels.
    /// The texture is released when the element leaves the panel and rebuilt on the next upload.
    /// </summary>
    public sealed class PixelImage : VisualElement
    {
        Texture2D _tex;
        Color32[] _flip;

        public PixelImage(string classes = null)
        {
            Ui.AddClasses(this, classes);
            pickingMode = PickingMode.Ignore;
            style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
            RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        public bool HasTexture => _tex != null;

        public void Upload(PixelBuffer b)
        {
            int w = b.W, h = b.H;
            if (_tex == null || _tex.width != w || _tex.height != h)
            {
                Release();
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                    name = "LE pixel image",
                };
                style.backgroundImage = new StyleBackground(Background.FromTexture2D(_tex));
            }
            if (_flip == null || _flip.Length != w * h) _flip = new Color32[w * h];
            // Buffers are top-down; textures are bottom-up.
            for (int y = 0; y < h; y++) System.Array.Copy(b.Px, y * w, _flip, (h - 1 - y) * w, w);
            _tex.SetPixels32(_flip);
            _tex.Apply(false, false);
        }

        public void Release()
        {
            if (_tex == null) return;
            style.backgroundImage = StyleKeyword.None;
            if (Application.isPlaying) Object.Destroy(_tex);
            else Object.DestroyImmediate(_tex);
            _tex = null;
        }
    }
}
