﻿using System;
using System.Collections.Generic;
using System.Text;
using CYQ.Data;
using CYQ.Data.Table;
using Aries.Core.DB;

namespace Aries.Core.Auth
{
    /// <summary>
    /// 系统菜单相关类
    /// </summary>
    public static partial class SysMenu
    {
        //private static MDataTable _MenuTable;
        /// <summary>
        /// 菜单表（全局缓存）
        /// </summary>
        public static MDataTable MenuTable
        {
            get
            {
                //if (_MenuTable == null)
                //{
                using (MAction action = new MAction(U_AriesEnum.Sys_Menu))
                {
                    return action.Select("order by MenuLevel ASC,SortOrder ASC");
                }
                //}
                //return _MenuTable;
            }
            //set
            //{
            //    _MenuTable = value;
            //}
        }

        //private static MDataTable _ActionTable;
        /// <summary>
        /// 菜单功能表（全局缓存）
        /// </summary>
        public static MDataTable ActionTable
        {
            get
            {
                //if (_ActionTable == null)
                //{
                using (MAction action = new MAction(U_AriesEnum.Sys_Action))
                {
                    return action.Select("order by SortOrder ASC");
                }
                //}
                //return _ActionTable;
            }
            //set
            //{
            //    _ActionTable = value;
            //}
        }



    }
    public static partial class SysMenu
    {
        /// <summary>
        /// 系统菜单（功能项变成子节点）
        /// </summary>
        public static MDataTable SysMenuAction
        {
            get
            {
                MDataTable dt;
                if (!UserAuth.IsSuperAdmin)
                {
                    dt = GetUserMenu(true);
                }
                else
                {
                    dt = MenuTable;
                }
                MDataTable action = ActionTable;
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    var row = dt.Rows[i];
                    string menuid = row.Get<string>("Menuid");
                    string actionids = row.Get<string>("Actionids");
                    if (!string.IsNullOrEmpty(actionids))
                    {
                        string[] ids = actionids.Split(',');
                        string refNames = string.Empty, actionNames = string.Empty;

                        foreach (string id in ids)
                        {
                            var aRow = action.FindRow("Actionid='" + id + "'");
                            if (aRow != null)
                            {
                                var dr = dt.NewRow(true);
                                dr.Set("Menuid", id);
                                dr.Set("ParentMenuid", menuid);
                                dr.Set("MenuName", aRow.Get<string>("ActionName"));
                                dr.Set("Actionids", "");
                                dr.Set("menuIcon", "icon-save");
                                // dt.Rows.Add(dr);
                            }
                        }
                    }
                }
                return dt;
            }
        }

        /// <summary>
        /// 当前登陆用户的菜单
        /// </summary>
        public static MDataTable UserMenu
        {
            get
            {
                return GetUserMenu(false);
            }
        }

        public static MDataTable GetUserMenu(bool onlyid)
        {
            string roleids = UserAuth.Roleids;
            if (!string.IsNullOrEmpty(roleids))
            {
                roleids = "Roleid in ('" + roleids.Replace(",", "','") + "')";
                MDataTable dt;
                using (MAction action = new MAction(U_AriesEnum.Sys_RoleAction))
                {
                    action.SetSelectColumns("Menuid", "Actionid");
                    dt = action.Select(roleids);
                }
                if (dt.Rows.Count > 0)
                {
                    Dictionary<string, string> dic = RoleActionToDic(dt, onlyid);
                    MDataTable menuDt = MenuTable;
                    #region 组合有权限的菜单
                    if (!onlyid)
                    {
                        menuDt.Columns.Add("ActionRefNames", System.Data.SqlDbType.NVarChar);
                    }
                    for (int i = 0; i < menuDt.Rows.Count; i++)
                    {
                        MDataRow row = menuDt.Rows[i];
                        string menuid = row.Get<string>("Menuid");
                        if (!dic.ContainsKey(menuid))
                        {
                            if (!HasChild(menuid, MenuTable, dic))
                            {
                                menuDt.Rows.RemoveAt(i);
                                i--;
                            }
                        }
                        else
                        {
                            if (onlyid)
                            {
                                row.Set("Actionids", dic[menuid]);
                            }
                            else
                            {
                                row.Set("ActionRefNames", dic[menuid]);
                            }
                        }
                    }
                    #endregion
                    return menuDt;
                }

            }
            return null;
        }

        /// <summary>
        /// 角色权限表转字典
        /// </summary>
        /// <param name="onlyid">默认需要引用名，true时引用id</param>
        /// <returns></returns>
        public static Dictionary<string, string> RoleActionToDic(MDataTable dt, bool onlyid = false)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            if (dt.Rows.Count > 0)
            {
                dt.Distinct();//去掉重复行。

                MDataTable actions = ActionTable;
                MDataTable menus = MenuTable;
                MDataRow menuRow = null;
                string mid = "";
                foreach (MDataRow row in dt.Rows)
                {
                    string menuid = row.Get<string>(Sys_RoleAction.Menuid);
                    if (menuid != mid)
                    {
                        mid = menuid;
                        menuRow = menus.FindRow(mid);
                    }
                    if (menuRow != null)
                    {
                        string actionid = row.Get<string>(Sys_RoleAction.Actionid);
                        MDataRow aRow = actions.FindRow(Sys_Action.Actionid + "='" + actionid + "'");
                        if (aRow != null && aRow.Get<bool>(Sys_Action.IsEnabled, true) && menuRow.Get<string>(Sys_Menu.Actionids).IndexOf(actionid) > -1)
                        {
                            if (dic.ContainsKey(menuid))
                            {
                                dic[menuid] = dic[menuid] + "," + (onlyid ? actionid : aRow.Get<string>(Sys_Action.ActionRefName).Trim());
                            }
                            else
                            {
                                dic.Add(menuid, (onlyid ? actionid : aRow.Get<string>(Sys_Action.ActionRefName).Trim()));
                            }
                        }
                    }
                }
            }
            return dic;
        }
        /// <summary>
        /// 是否拥有下级的菜单权限
        /// </summary>
        /// <param name="menuid">当前菜单id</param>
        /// <param name="menuDt">整个菜单表</param>
        /// <param name="dic">当前用户拥有权限的的菜单</param>
        /// <returns></returns>
        private static bool HasChild(string menuid, MDataTable menuDt, Dictionary<string, string> dic)
        {
            MDataRowCollection childs = menuDt.FindAll("ParentMenuid='" + menuid + "'");
            if (childs != null && childs.Count > 0)
            {
                bool result = false;
                foreach (MDataRow row in childs)
                {
                    menuid = row.Get<string>("Menuid");
                    result = dic.ContainsKey(menuid);
                    if (!result)
                    {
                        result = HasChild(menuid, menuDt, dic);
                    }
                    if (result)
                    {
                        return result;
                    }
                }
            }
            return false;
        }

    }
}
