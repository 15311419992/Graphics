﻿using System.Collections;
using System.Collections.Generic;

namespace UnityEditor.ShaderGraph.Internal
{
    public class DependencyCollection : IEnumerable<DependencyCollection.Item>
    {
        public class Item 
        {
            public FieldDependency dependency { get; }

            public Item(FieldDependency dependency)
            {
                this.dependency = dependency;
            }
        }

        readonly List<DependencyCollection.Item> m_Items;

        public DependencyCollection()
        {
            m_Items = new List<DependencyCollection.Item>();
        }

        public void Add(DependencyCollection dependencies)
        {
            foreach(DependencyCollection.Item item in dependencies)
            {
                m_Items.Add(item);
            }
        }

        public void Add(FieldDependency dependency)
        {
            m_Items.Add(new Item(dependency));
        }

        public IEnumerator<DependencyCollection.Item> GetEnumerator()
        {
            return m_Items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
