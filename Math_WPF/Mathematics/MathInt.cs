using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Math_WPF.Mathematics
{
    #region struct: VectorInt2

    public struct VectorInt2 : IComparable<VectorInt2>, IComparable, IEquatable<VectorInt2>
    {
        #region Constructor

        public VectorInt2(int x, int y)
        {
            _x = x;
            _y = y;
        }

        #endregion

        #region IComparable<VectorInt2> Members

        public int CompareTo(VectorInt2 other)
        {
            // X then Y
            int retVal = _x.CompareTo(other.X);
            if (retVal != 0)
            {
                return retVal;
            }

            return _y.CompareTo(other.Y);
        }

        #endregion
        #region IComparable Members

        public int CompareTo(object obj)
        {
            if (obj is VectorInt2)
            {
                return CompareTo((VectorInt2)obj);
            }
            else
            {
                return 1;
            }
        }

        #endregion
        #region IEquatable<VectorInt2> Members

        public static bool Equals(VectorInt2 vector1, VectorInt2 vector2)
        {

            // struct doesn't need a null check

            // If both are null, or both are same instance, return true.
            //if (System.Object.ReferenceEquals(vector1, vector2))      
            //{
            //    return true;
            //}

            //if (vector1 == null && vector2 == null)       // this == calls VectorInt's == operator overload, which comes back here...stack overflow
            //if ((object)vector1 == null && (object)vector2 == null)
            //{
            //    return true;
            //}
            //else if ((object)vector1 == null || (object)vector2 == null)
            //{
            //    return false;
            //}


            return vector1.X == vector2.X && vector1.Y == vector2.Y;
        }
        public bool Equals(VectorInt2 vector)
        {
            return VectorInt2.Equals(this, vector);
        }
        public override bool Equals(object obj)
        {
            if (!(obj is VectorInt2))
            {
                return false;
            }

            return VectorInt2.Equals(this, (VectorInt2)obj);
        }

        public override int GetHashCode()
        {
            return (_x, _y).GetHashCode();
        }

        #endregion

        #region Public Properties

        private int _x;
        public int X
        {
            get
            {
                return _x;
            }
            set
            {
                _x = value;
            }
        }

        private int _y;
        public int Y
        {
            get
            {
                return _y;
            }
            set
            {
                _y = value;
            }
        }

        #endregion

        #region Operator Overloads

        public static VectorInt2 operator -(VectorInt2 vector)
        {
            return new VectorInt2(-vector.X, -vector.Y);
        }
        public static VectorInt2 operator -(VectorInt2 vector1, VectorInt2 vector2)
        {
            return new VectorInt2(vector1.X - vector2.X, vector1.Y - vector2.Y);
        }
        public static VectorInt2 operator *(int scalar, VectorInt2 vector)
        {
            return new VectorInt2(vector.X * scalar, vector.Y * scalar);
        }
        public static VectorInt2 operator *(VectorInt2 vector, int scalar)
        {
            return new VectorInt2(vector.X * scalar, vector.Y * scalar);
        }
        public static VectorInt2 operator *(float scalar, VectorInt2 vector)
        {
            return new VectorInt2((vector.X * scalar).ToInt_Round(), (vector.Y * scalar).ToInt_Round());
        }
        public static VectorInt2 operator *(VectorInt2 vector, float scalar)
        {
            return new VectorInt2((vector.X * scalar).ToInt_Round(), (vector.Y * scalar).ToInt_Round());
        }
        public static VectorInt2 operator *(double scalar, VectorInt2 vector)
        {
            return new VectorInt2((vector.X * scalar).ToInt_Round(), (vector.Y * scalar).ToInt_Round());
        }
        public static VectorInt2 operator *(VectorInt2 vector, double scalar)
        {
            return new VectorInt2((vector.X * scalar).ToInt_Round(), (vector.Y * scalar).ToInt_Round());
        }
        public static VectorInt2 operator /(VectorInt2 vector, int scalar)
        {
            return new VectorInt2(vector.X / scalar, vector.Y / scalar);
        }
        public static VectorInt2 operator /(VectorInt2 vector, float scalar)
        {
            return new VectorInt2((vector.X / scalar).ToInt_Round(), (vector.Y / scalar).ToInt_Round());
        }
        public static VectorInt2 operator /(VectorInt2 vector, double scalar)
        {
            return new VectorInt2((vector.X / scalar).ToInt_Round(), (vector.Y / scalar).ToInt_Round());
        }
        public static VectorInt2 operator +(VectorInt2 vector1, VectorInt2 vector2)
        {
            return new VectorInt2(vector1.X + vector2.X, vector1.Y + vector2.Y);
        }
        public static bool operator ==(VectorInt2 vector1, VectorInt2 vector2)
        {
            return vector1.X == vector2.X && vector1.Y == vector2.Y;
        }
        public static bool operator !=(VectorInt2 vector1, VectorInt2 vector2)
        {
            return vector1.X != vector2.X || vector1.Y != vector2.Y;
        }

        #endregion
        #region Public Methods

        public override string ToString()
        {
            return $"{_x}, {_y}";
        }

        #endregion
    }

    #endregion
    #region struct: VectorInt3

    public struct VectorInt3 : IComparable<VectorInt3>, IComparable, IEquatable<VectorInt3>
    {
        #region Constructor

        public VectorInt3(int x, int y, int z)
        {
            _x = x;
            _y = y;
            _z = z;
        }

        #endregion

        #region IComparable<VectorInt3> Members

        public int CompareTo(VectorInt3 other)
        {
            // X then Y then Z
            int retVal = _x.CompareTo(other.X);
            if (retVal != 0)
                return retVal;

            retVal = _y.CompareTo(other.Y);
            if (retVal != 0)
                return retVal;

            return _z.CompareTo(other.Z);
        }

        #endregion
        #region IComparable Members

        public int CompareTo(object obj)
        {
            if (obj is VectorInt3 cast)
            {
                return CompareTo(cast);
            }
            else
            {
                return 1;
            }
        }

        #endregion
        #region IEquatable<VectorInt3> Members

        public static bool Equals(VectorInt3 vector1, VectorInt3 vector2)
        {

            // struct doesn't need a null check

            // If both are null, or both are same instance, return true.
            //if (System.Object.ReferenceEquals(vector1, vector2))      
            //{
            //    return true;
            //}

            //if (vector1 == null && vector2 == null)       // this == calls VectorInt's == operator overload, which comes back here...stack overflow
            //if ((object)vector1 == null && (object)vector2 == null)
            //{
            //    return true;
            //}
            //else if ((object)vector1 == null || (object)vector2 == null)
            //{
            //    return false;
            //}


            return vector1.X == vector2.X && vector1.Y == vector2.Y && vector1.Z == vector2.Z;
        }
        public bool Equals(VectorInt3 vector)
        {
            return VectorInt3.Equals(this, vector);
        }
        public override bool Equals(object obj)
        {
            if (!(obj is VectorInt3))
                return false;

            return VectorInt3.Equals(this, (VectorInt3)obj);
        }

        public override int GetHashCode()
        {
            return (_x, _y, _z).GetHashCode();
        }

        #endregion

        #region Public Properties

        private int _x;
        public int X
        {
            get
            {
                return _x;
            }
            set
            {
                _x = value;
            }
        }

        private int _y;
        public int Y
        {
            get
            {
                return _y;
            }
            set
            {
                _y = value;
            }
        }

        private int _z;
        public int Z
        {
            get
            {
                return _z;
            }
            set
            {
                _z = value;
            }
        }

        #endregion

        #region Operator Overloads

        public static VectorInt3 operator -(VectorInt3 vector)
        {
            return new VectorInt3(-vector.X, -vector.Y, -vector.Z);
        }
        public static VectorInt3 operator -(VectorInt3 vector1, VectorInt3 vector2)
        {
            return new VectorInt3(vector1.X - vector2.X, vector1.Y - vector2.Y, vector1.Z - vector2.Z);
        }
        public static VectorInt3 operator *(int scalar, VectorInt3 vector)
        {
            return new VectorInt3(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);
        }
        public static VectorInt3 operator *(VectorInt3 vector, int scalar)
        {
            return new VectorInt3(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);
        }
        public static VectorInt3 operator *(float scalar, VectorInt3 vector)
        {
            return new VectorInt3((vector.X * scalar).ToInt_Round(), (vector.Y * scalar).ToInt_Round(), (vector.Z * scalar).ToInt_Round());
        }
        public static VectorInt3 operator *(VectorInt3 vector, float scalar)
        {
            return new VectorInt3((vector.X * scalar).ToInt_Round(), (vector.Y * scalar).ToInt_Round(), (vector.Z * scalar).ToInt_Round());
        }
        public static VectorInt3 operator *(double scalar, VectorInt3 vector)
        {
            return new VectorInt3((vector.X * scalar).ToInt_Round(), (vector.Y * scalar).ToInt_Round(), (vector.Z * scalar).ToInt_Round());
        }
        public static VectorInt3 operator *(VectorInt3 vector, double scalar)
        {
            return new VectorInt3((vector.X * scalar).ToInt_Round(), (vector.Y * scalar).ToInt_Round(), (vector.Z * scalar).ToInt_Round());
        }
        public static VectorInt3 operator /(VectorInt3 vector, int scalar)
        {
            return new VectorInt3(vector.X / scalar, vector.Y / scalar, vector.Z / scalar);
        }
        public static VectorInt3 operator /(VectorInt3 vector, float scalar)
        {
            return new VectorInt3((vector.X / scalar).ToInt_Round(), (vector.Y / scalar).ToInt_Round(), (vector.Z / scalar).ToInt_Round());
        }
        public static VectorInt3 operator /(VectorInt3 vector, double scalar)
        {
            return new VectorInt3((vector.X / scalar).ToInt_Round(), (vector.Y / scalar).ToInt_Round(), (vector.Z / scalar).ToInt_Round());
        }
        public static VectorInt3 operator +(VectorInt3 vector1, VectorInt3 vector2)
        {
            return new VectorInt3(vector1.X + vector2.X, vector1.Y + vector2.Y, vector1.Z + vector2.Z);
        }
        public static bool operator ==(VectorInt3 vector1, VectorInt3 vector2)
        {
            return vector1.X == vector2.X && vector1.Y == vector2.Y && vector1.Z == vector2.Z;
        }
        public static bool operator !=(VectorInt3 vector1, VectorInt3 vector2)
        {
            return vector1.X != vector2.X || vector1.Y != vector2.Y || vector1.Z != vector2.Z;
        }

        #endregion
        #region Public Methods

        public override string ToString()
        {
            return $"{_x}, {_y}, {_z}";
        }

        #endregion
    }

    #endregion

    #region struct: RectInt2

    public struct RectInt2
    {
        #region Constructor

        public RectInt2(int width, int height)
        {
            _x = 0;
            _y = 0;
            _width = width;
            _height = height;
        }
        public RectInt2(VectorInt2 size)
        {
            _x = 0;
            _y = 0;
            _width = size.X;
            _height = size.Y;
        }

        public RectInt2(VectorInt2 location, VectorInt2 size)
        {
            _x = location.X;
            _y = location.Y;
            _width = size.X;
            _height = size.Y;
        }
        public RectInt2(int x, int y, int width, int height)
        {
            _x = x;
            _y = y;
            _width = width;
            _height = height;
        }

        #endregion

        #region Public Properties

        private int _x;
        public int X
        {
            get
            {
                return _x;
            }
            set
            {
                _x = value;
            }
        }

        private int _y;
        public int Y
        {
            get
            {
                return _y;
            }
            set
            {
                _y = value;
            }
        }

        private int _width;
        public int Width
        {
            get
            {
                return _width;
            }
            set
            {
                _width = value;
            }
        }

        private int _height;
        public int Height
        {
            get
            {
                return _height;
            }
            set
            {
                _height = value;
            }
        }

        public int Left => _x;
        public int Right => _x + _width;
        public int Top => _y;
        public int Bottom => _y + _height;

        public VectorInt2 Position => new VectorInt2(_x, _y);
        public VectorInt2 Size => new VectorInt2(_width, _height);

        #endregion

        #region Public Methods

        public static RectInt2? Intersect(RectInt2 rect1, RectInt2 rect2)
        {
            if (rect1.Right <= rect2.Left || rect2.Right <= rect1.Left || rect1.Bottom <= rect2.Top || rect2.Bottom <= rect1.Top)
            {
                return null;
            }

            int left = Math.Max(rect1.Left, rect2.Left);
            int top = Math.Max(rect1.Top, rect2.Top);

            int right = Math.Min(rect1.Right, rect2.Right);
            int bottom = Math.Min(rect1.Bottom, rect2.Bottom);

            return new RectInt2(left, top, right - left, bottom - top);
        }

        public bool Contains(int x, int y)
        {
            return x >= Left && x <= Right && y >= Top && y <= Bottom;
        }
        public bool Contains(VectorInt2 point)
        {
            return point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
        }

        public override string ToString()
        {
            return $"pos({_x}, {_y}) size({_width}, {_height})";
        }

        #endregion
    }

    #endregion
    #region struct: RectInt3

    public struct RectInt3
    {
        #region Constructor

        public RectInt3(int sizeX, int sizeY, int sizeZ)
        {
            _x = 0;
            _y = 0;
            _z = 0;
            _sizeX = sizeX;
            _sizeY = sizeY;
            _sizeZ = sizeZ;
        }
        public RectInt3(VectorInt3 size)
        {
            _x = 0;
            _y = 0;
            _z = 0;
            _sizeX = size.X;
            _sizeY = size.Y;
            _sizeZ = size.Z;
        }

        public RectInt3(VectorInt3 location, VectorInt3 size)
        {
            _x = location.X;
            _y = location.Y;
            _z = location.Z;

            _sizeX = size.X;
            _sizeY = size.Y;
            _sizeZ = size.Z;
        }
        public RectInt3(int x, int y, int z, int sizeX, int sizeY, int sizeZ)
        {
            _x = x;
            _y = y;
            _z = z;

            _sizeX = sizeX;
            _sizeY = sizeY;
            _sizeZ = sizeZ;
        }

        #endregion

        #region Public Properties

        private int _x;
        public int X
        {
            get
            {
                return _x;
            }
            set
            {
                _x = value;
            }
        }

        private int _y;
        public int Y
        {
            get
            {
                return _y;
            }
            set
            {
                _y = value;
            }
        }

        private int _z;
        public int Z
        {
            get
            {
                return _z;
            }
            set
            {
                _z = value;
            }
        }

        private int _sizeX;
        public int SizeX
        {
            get
            {
                return _sizeX;
            }
            set
            {
                _sizeX = value;
            }
        }

        private int _sizeY;
        public int SizeY
        {
            get
            {
                return _sizeY;
            }
            set
            {
                _sizeY = value;
            }
        }

        private int _sizeZ;
        public int SizeZ
        {
            get
            {
                return _sizeZ;
            }
            set
            {
                _sizeZ = value;
            }
        }

        public int MinX => _x;
        public int MaxX => _x + _sizeX;

        public int MinY => _y;
        public int MaxY => _y + _sizeY;

        public int MinZ => _z;
        public int MaxZ => _z + _sizeZ;

        public VectorInt3 Position => new VectorInt3(_x, _y, _z);
        public VectorInt3 Size => new VectorInt3(_sizeX, _sizeY, _sizeZ);

        #endregion

        #region Public Methods

        public static RectInt3? Intersect(RectInt3 rect1, RectInt3 rect2)
        {
            if (rect1.MaxX <= rect2.MinX ||
                rect2.MaxX <= rect1.MinX ||
                rect1.MaxY <= rect2.MinY ||
                rect2.MaxY <= rect1.MinY ||
                rect1.MaxZ <= rect2.MinZ ||
                rect2.MaxZ <= rect1.MinZ)
            {
                return null;
            }

            int minX = Math.Max(rect1.MinX, rect2.MinX);
            int minY = Math.Max(rect1.MinY, rect2.MinY);
            int minZ = Math.Max(rect1.MinZ, rect2.MinZ);

            int maxX = Math.Min(rect1.MaxX, rect2.MaxX);
            int maxY = Math.Min(rect1.MaxY, rect2.MaxY);
            int maxZ = Math.Min(rect1.MaxZ, rect2.MaxZ);

            return new RectInt3(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
        }

        public bool Contains(int x, int y, int z)
        {
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;
        }
        public bool Contains(VectorInt3 point)
        {
            return point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY && point.Z >= MinZ && point.Z <= MaxZ;
        }

        public override string ToString()
        {
            return $"pos({_x}, {_y}, {_z}) size({_sizeX}, {_sizeY}, {_sizeZ})";
        }

        #endregion
    }

    #endregion
}
