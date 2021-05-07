using System;
using System.Collections.Generic;

namespace Machete
{
    public class Vector : IEquatable<Vector>, ICloneable
    {
        public List<double> Data { get; set; }

        public int Size
        {
            get { return Data.Count; }
        }

        public Vector(int m) : this(0, m) { }

        public Vector(double constant, int m)
        {
            Data = new List<double>();

            for (int i = 0; i < m; i++)
                Data.Add(constant);
        }

        public Vector(List<double> other)
        {
            int m = other.Count;

            Data = new List<double>();

            for (int i = 0; i < m; i++)
                Data.Add(other[i]);
        }

        public Vector(Vector a, Vector b, double t)
        {
            int m = a.Data.Count;

            if (a.Data.Count != b.Data.Count)
                throw new ArgumentException("The size of the two vectors must be equal");

            Data = new List<double>();

            for (int i = 0; i < m; i++)
            {
                double d = (1.0 - t) * a.Data[i];
                d += t * b.Data[i];
                Data.Add(d);
            }
        }

        public double this[int index]
        {
            get
            {
                return Data[index];
            }

            set
            {
                Data[index] = value;
            }
        }

        public void Set(double rhs)
        {
            for (int i = 0; i < Size; i++)
                Data[i] = rhs;
        }

        public static Vector operator -(Vector vec)
        {
            Vector result = new Vector(vec.Size);

            for (int i = 0; i < result.Size; i++)
                result[i] = -vec[i];

            return result;
        }

        public static Vector operator *(Vector vec, double lhs)
        {
            Vector result = new Vector(vec.Size);

            for (int i = 0; i < result.Size; i++)
                result[i] = vec[i] * lhs;

            return result;
        }

        public static Vector operator /(Vector vec, double lhs)
        {
            Vector result = new Vector(vec.Size);

            for (int i = 0; i < result.Size; i++)
                result[i] = vec[i] / lhs;

            return result;
        }

        public static Vector operator /(Vector vec, Vector vec2)
        {
            Vector result = new Vector(vec.Size);

            for (int i = 0; i < result.Size; i++)
                result[i] = vec[i] / vec2[i];

            return result;
        }

        public static Vector operator +(Vector vec, Vector lhs)
        {
            Vector result = new Vector(vec.Size);

            for (int i = 0; i < result.Size; i++)
                result[i] = vec[i] + lhs[i];

            return result;
        }

        public static Vector operator -(Vector vec, Vector lhs)
        {
            Vector result = new Vector(vec.Size);

            for (int i = 0; i < result.Size; i++)
                result[i] = vec[i] - lhs[i];

            return result;
        }

        public static bool operator ==(Vector left, Vector right)
        {
            if (object.ReferenceEquals(left, null) && object.ReferenceEquals(right, null))
                return true;

            return left.Equals(right);
        }

        public static bool operator !=(Vector left, Vector right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            int result = this[0].GetHashCode();

            for (int i = 1; i < Size; i++)
                result ^= this[i].GetHashCode();

            return result;
        }

        public double L2Norm2(Vector other)
        {
            double ret = 0;

            for (int i = 0; i < Size; i++)
            {
                double delta = Data[i] - other[i];
                ret += delta * delta;
            }

            return ret;
        }

        public double L2Norm(Vector other)
        {
            return Math.Sqrt(L2Norm2(other));
        }

        public double L2Norm()
        {
            double ret = 0.0;

            for (int ii = 0;
                ii < this.Size;
                ii++)
            {
                ret += this.Data[ii] * this.Data[ii];
            }

            return Math.Sqrt(ret);
        }

        public double Length()
        {
            double ret = 0;

            for (int i = 0; i < Size; i++)
                ret += Data[i] * Data[i];

            return Math.Sqrt(ret);
        }

        public Vector Normalize()
        {
            double length = Length();

            for (int i = 0; i < Size; i++)
            {
                Data[i] /= length;
            }

            return this;
        }

        public double Dot(Vector rhs)
        {
            double ret = 0;

            for (int i = 0; i < Size; i++)
                ret += Data[i] * rhs[i];

            return ret;
        }

        public double Sum()
        {
            double ret = 0;

            for (int i = 0; i < Size; i++)
                ret += Data[i];

            return ret;
        }

        public void CumulativeSum()
        {
            double sum = 0;

            for (int i = 0; i < Size; i++)
            {
                sum += Data[i];
                Data[i] = sum;
            }
        }

        public bool Equals(Vector other)
        {
            for (int i = 0; i < Size; i++)
                if (this[i] != other[i])
                    return false;

            return true;
        }

        public override bool Equals(object other)
        {
            if (!(other is Vector))
                return false;

            Vector temp = other as Vector;
            return Equals(temp);
        }

        public object Clone()
        {
            return new Vector(this.Data);
        }

        public bool isZero()
        {
            for (int ii = 0;
                ii < this.Size;
                ii++)
            {
                if (this.Data[ii] != 0.0)
                    return false;
            }

            return true;
        }

        /**
         * Store component-wise minimum of the two
         */
        public void Minimum(Vector other)
        {
            for (int ii = 0;
                ii < this.Size;
                ii++)
            {
                if (other.Data[ii] < this.Data[ii])
                {
                    this.Data[ii] = other.Data[ii];
                }
            }
        }

        /**
         * Store the component-wise maximum of the two
         */
        public void Maximum(Vector other)
        {
            for (int ii = 0;
                ii < this.Size;
                ii++)
            {
                if (other.Data[ii] > this.Data[ii])
                {
                    this.Data[ii] = other.Data[ii];
                }
            }
        }
    }
}
