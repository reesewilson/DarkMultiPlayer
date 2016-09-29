using System;

namespace KSPIRC
{
    /// <summary>
    /// KSPAddon with equality checking using an additional type parameter. Fixes the issue where
    /// AddonLoader prevents multiple start-once addons with the same start scene.
    /// </summary>

    public class KSPIRCKSPAddonFixed : KSPAddon, IEquatable<KSPIRCKSPAddonFixed>
    {
        private readonly Type type;

        public KSPIRCKSPAddonFixed(KSPAddon.Startup startup, bool once, Type type)
            : base(startup, once)
        {
            this.type = type;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType())
            {
                return false;
            }
            return Equals((KSPIRCKSPAddonFixed)obj);
        }

        public bool Equals(KSPIRCKSPAddonFixed other)
        {
            if (this.once != other.once)
            {
                return false;
            }
            if (this.startup != other.startup)
            {
                return false;
            }
            if (this.type != other.type)
            {
                return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return this.startup.GetHashCode() ^ this.once.GetHashCode() ^ this.type.GetHashCode();
        }
    }
}