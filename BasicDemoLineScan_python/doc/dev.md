# Development Documentation

## Tips
1. The SDK directory is located in `/opt/MVS`
2. `/opt/MVS/doc`Contains detailed SDK interface description
   .xlsx is the optional parameter description of the camera
3. `/opt/MVS/doc/samples`Contains code examples
4. For example  `python/MvImport/MvCameraControl_class.py` Contains Python adjustable class. (Can be refactored according to C code as needed).
5. The camera only caches settings related content. The pictures are cached in the MVS SDK. 
Code running requires memory space to be allocated. Please note that it is released after calling to avoid memory leaks.
6. use `python -m hik_camera.bandwidth` Monitor network port traffic


## TODO
- [x] python setup.py install cannot find xls
- [x] Abnormal reset camera
   - self.MV_CC_SetCommandValue("DeviceReset")
- [x] Self-developed fast automatic exposure algorithm?
   - Automatic exposure RoI
- [x] learn rawpy of rgb=re['raw_obj'].postprocess(), Try to speed up the conversion to RGB
   - No faster than acquiring images
- [x] ~~ Should we consider multi-frame fusion HDR?~~
   - 12bit raw The image can capture both bright and dark details
- [ ] Run through ParametrizeCamera_LoadAndSave.py MV_CC_FeatureSave
- [ ] DNG Support the right meta information


## Image storage space
```bash
# for 12 bit of raw12 picture, The space occupied by different storage forms
>>> tree-raw12
└── /: (3036, 4024)uint16

412K	./color.jpg  # uint8
8.7M	./color.png  # uint8
14M	./raw.png    # uint16
15M	./uint16.npz
18M	./int32.npz
24M	./uint16.pkl
47M	./int32.pkl
```

