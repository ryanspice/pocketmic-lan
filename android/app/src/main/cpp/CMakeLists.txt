cmake_minimum_required(VERSION 3.22.1)
project("pocketmic" C)

# ---------------------------------------------------------------------------
# libopus – prebuilt per-ABI static library
#
# Place the prebuilt archives at:
#   app/src/main/jniLibs/<abi>/libopus.a
#
# Build them from the opus source tree with the Android NDK toolchain, e.g.:
#   for abi in arm64-v8a armeabi-v7a x86_64; do
#     cmake -B build-$abi -DCMAKE_TOOLCHAIN_FILE=$NDK/build/cmake/android.toolchain.cmake \
#           -DANDROID_ABI=$abi -DANDROID_PLATFORM=android-26 \
#           -DOPUS_BUILD_SHARED_LIBRARY=OFF -DOPUS_BUILD_TESTING=OFF \
#           -DBUILD_SHARED_LIBS=OFF <opus-src>
#     cmake --build build-$abi -j$(nproc)
#   done
#
# The header lives at app/src/main/cpp/include/opus/opus.h (copy from opus/include/).
# ---------------------------------------------------------------------------

add_library(opus SHARED IMPORTED)
set_target_properties(opus PROPERTIES
    IMPORTED_LOCATION ${CMAKE_SOURCE_DIR}/../jniLibs/${ANDROID_ABI}/libopus.so
)

# If you prefer a static link, replace the above with:
# add_library(opus STATIC IMPORTED)
# set_target_properties(opus PROPERTIES
#     IMPORTED_LOCATION ${CMAKE_SOURCE_DIR}/../jniLibs/${ANDROID_ABI}/libopus.a
# )

add_library(pocketmic_opus SHARED opus_jni.c)

target_include_directories(pocketmic_opus PRIVATE
    ${CMAKE_SOURCE_DIR}/include
)

target_link_libraries(pocketmic_opus
    opus
    android
    log
)
