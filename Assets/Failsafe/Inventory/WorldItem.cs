// WorldItem.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Failsafe.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class WorldItem : MonoBehaviour
    {
        public ItemDefinition definition;
        [Min(1)] public int amount = 1;

        [Header("Legacy Actions (optional)")]
        public List<ActionsGroup> actionsGroups; // можно не использовать

        private Rigidbody _rb; private Collider _col;
        private static readonly Guid PlayerUseActionId = new Guid("316f217b-db19-4ab3-992d-f06d0052d966");

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>(); if (_col==null) _col = gameObject.AddComponent<BoxCollider>();
        }

        public void BindFromInstance(ItemInstance inst){ if(inst==null) return; definition=inst.Def; amount=Mathf.Max(1,inst.Stack); }

        public void ToInventoryState(){ if(_rb){ _rb.linearVelocity=Vector3.zero; _rb.angularVelocity=Vector3.zero; _rb.isKinematic=true; } if(_col) _col.enabled=false; }
        public void ToWorldState(Vector3? vel=null, Vector3? ang=null){ if(_rb){ _rb.isKinematic=false; if(vel.HasValue)_rb.linearVelocity=vel.Value; if(ang.HasValue)_rb.angularVelocity=ang.Value; } if(_col) _col.enabled=true; }

        public bool IsUsable(){ if(actionsGroups==null||actionsGroups.Count==0) return false; return actionsGroups.Any(g=>g.Actions.Any(a=>a.action.id==PlayerUseActionId)); }
        public void Use(){ if(actionsGroups==null) return; foreach(var g in actionsGroups.Where(g=>g.Actions.Any(a=>a.action.id==PlayerUseActionId))) g.Invoke(); }

        // Интеграция с интеракцией игрока
        public bool TryPickupTap()
        {
            var ic=InventoryController.Instance; if(ic==null||definition==null) return false;
            var inst=ic.Service.Create(definition,amount);
            if(!ic.Service.TryAdd(ic.playerGridId,inst)){ ic.Model.Instances.Remove(inst.Id); return false; }
            ToInventoryState(); Destroy(gameObject); return true;
        }

        public bool TryPickupHold() // удержание E -> квикбар
        {
            var ic=InventoryController.Instance; if(ic==null||definition==null) return false;
            var inst=ic.Service.Create(definition,amount);
            if(ic.Service.TryAssignQuickbarNext(inst) || ic.Service.TryAdd(ic.playerGridId,inst))
            { ToInventoryState(); Destroy(gameObject); return true; }
            ic.Model.Instances.Remove(inst.Id); return false;
        }
    }
}