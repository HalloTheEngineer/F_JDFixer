namespace JDFixer.Configuration
{
    /// <summary>
    /// One NJS-keyed JD preference. Bound directly to BSIPA config storage, so the property names and
    /// accessibility are part of the on-disk format and must not change.
    /// </summary>
    internal class JDPref
    {
        internal virtual float njs { get; set; } = 16f;
        internal virtual float jumpDistance { get; set; } = 18f;

        public JDPref()
        {
        }

        internal JDPref(float njs, float jumpDistance)
        {
            this.njs = njs;
            this.jumpDistance = jumpDistance;
        }
    }

    /// <summary>
    /// One NJS-keyed RT preference. Bound directly to BSIPA config storage; see <see cref="JDPref"/>
    /// for why the shape is fixed.
    /// </summary>
    internal class RTPref
    {
        internal virtual float njs { get; set; } = 16f;
        internal virtual float reactionTime { get; set; } = 800f;

        public RTPref()
        {
        }

        internal RTPref(float njs, float reactionTime)
        {
            this.njs = njs;
            this.reactionTime = reactionTime;
        }
    }
}
