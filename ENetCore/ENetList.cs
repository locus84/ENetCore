using System;
using System.Collections.Generic;
using System.Text;

namespace ENetCore
{
    public class ENetListNode<T> where T : ENetListNode<T>, new()
    {
        static Queue<T> m_Pool = new Queue<T>();

        public T Previous;
        public T Next;

        public static T Create()
        {
            if (m_Pool.Count > 0) return m_Pool.Dequeue();
            return new T();
        }

        public static void Free(T toFree)
        {
            m_Pool.Enqueue(toFree);
        }
    }

    public struct ENetList<T> where T : ENetListNode<T>, new()
    {
        public T Sentinel;

        public static T Insert(T position, T data)
        {
            data.Previous = position.Previous;
            data.Next = position;

            data.Previous.Next = data;
            position.Previous = data;
            return data;
        }

        public static T Move(T position, T dataFirst, T dataLast)
        {
            dataFirst.Previous.Next = dataLast.Next;
            dataLast.Next.Previous = dataFirst.Previous;

            dataFirst.Previous = position.Previous;
            dataLast.Next = position;

            dataFirst.Previous.Next = dataFirst;
            position.Previous = dataLast;
            return dataFirst;
        }

        public static T Remove(T position)
        {
            position.Previous.Next = position.Next;
            position.Next.Previous = position.Previous;
            return position;
        }

        public void Clear()
        {
            Sentinel.Next = Sentinel;
            Sentinel.Previous = Sentinel;
        }

        public int GetSize()
        {
            int size = 0;
            for (var pos = Sentinel.Next; pos != Sentinel; pos = pos.Next)
                size++;
            return size;
        }

        public T Begin
        {
            get { return Sentinel.Next; }
        }

        public T End
        {
            get { return Sentinel; }
        }

        public T Front
        {
            get { return Sentinel.Next; }
        }

        public T Back
        {
            get { return Sentinel.Previous; }
        }

        public bool IsEmpty
        {
            get { return Begin == End; }
        }
    }
}
