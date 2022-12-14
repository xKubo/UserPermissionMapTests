using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DBOUtils
{

    using UserPermission = List<int>;
    using FGetUserRights = Func<int, List<int>>;



    public class UsersPermissionsMap
    {
        enum Status
        {
            Empty,
            Pending,
            Created,
        }

        class LockedUserPermission
        {
            public UserPermission GetPermission()
            {
                if (m_Exception!=null)
                    throw m_Exception;
                return m_Permission;
            }

            public object m_Lock = new object();
            public UserPermission m_Permission = null;
            public Status m_Status = Status.Empty;
            public Exception m_Exception = null;
        }

        public UsersPermissionsMap(FGetUserRights f)
        {
            m_FGetRights = f;
        }

        public UserPermission GetUserPermission(int IDtUser)
        {
            LockedUserPermission lup;
            lock (m_Lock)
            {
                if (!m_UsersPermissions.TryGetValue(IDtUser, out lup))
                {
                    lup = new LockedUserPermission();
                    m_UsersPermissions.Add(IDtUser, lup);
                }
            }

            lock (lup.m_Lock)
            {
                if (lup.m_Status == Status.Created)
                {
                    return lup.GetPermission();
                }

                if (lup.m_Status == Status.Pending)
                {
                    while (lup.m_Status != Status.Created) // I am not sure about spurious wakeups
                        Monitor.Wait(lup.m_Lock);           // wait until someone updates the data
                    return lup.GetPermission();
                }
                else
                    lup.m_Status = Status.Pending;
            }

            UserPermission p = null;
            try
            {
                p = m_FGetRights(IDtUser);

                lock (lup.m_Lock)
                {
                    lup.m_Status = Status.Created;
                    lup.m_Permission = p;
                    lup.m_Exception = null;
                    Monitor.PulseAll(lup.m_Lock);
                }

            }
            catch (Exception e)
            {

                lock (lup.m_Lock)
                {
                    lup.m_Status = Status.Created;
                    lup.m_Exception = e;
                    lup.m_Permission = null;
                    Monitor.PulseAll(lup.m_Lock);
                }

                throw;

            }

            return p;
        }

        private FGetUserRights m_FGetRights;
        private Dictionary<int, LockedUserPermission> m_UsersPermissions = new Dictionary<int, LockedUserPermission>();
        private object m_Lock = new object();
    }
}
