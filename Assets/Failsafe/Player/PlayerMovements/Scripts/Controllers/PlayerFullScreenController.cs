using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Failsafe.PlayerMovements.Controllers
{
    /// <summary>
    /// Контроллер режима, при котором камера занимает весь экран.
    /// </summary>
    public class PlayerFullScreenController
    {
        private Transform _playerCamera;
        private Transform _tempCameraPoint;
        private Transform _cameraPoint;
        private bool _isInFullScreen = false;


        public PlayerFullScreenController(Transform playerCamera)
        {
            _playerCamera = playerCamera;
        }

        public void Enter()
        {
            Debug.Log("Enter " + nameof(PlayerFullScreenController));
            _isInFullScreen = true;
        }

        public void Exit()
        {
            Debug.Log("Exit " + nameof(PlayerFullScreenController));
            _isInFullScreen = false;
        }
    }
}