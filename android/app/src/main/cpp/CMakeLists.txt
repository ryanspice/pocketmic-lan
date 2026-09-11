cmake_minimum_required(VERSION 3.22.1)
project("pocketmic" C CXX)

# ---------------------------------------------------------------------------
# PocketMic Android native build
#
# Builds libopus from source (pinned at 1.5.2) and the opus_jni wrapper.
# The opus source tree must be at app/src/main/cpp/opus/
# ---------------------------------------------------------------------------

# --- libopus (built from source) ---

set(OPUS_SOURCE_DIR "${CMAKE_SOURCE_DIR}/opus")

# Disable everything we don't need
set(OPUS_BUILD_SHARED_LIBRARY ON CACHE BOOL "" FORCE)
set(OPUS_BUILD_TESTING OFF CACHE BOOL "" FORCE)
set(OPUS_BUILD_PROGRAMS OFF CACHE BOOL "" FORCE)
set(OPUS_INSTALL_PKG_CONFIG_MODULE OFF CACHE BOOL "" FORCE)
set(OPUS_INSTALL_CMAKE_CONFIG_MODULE OFF CACHE BOOL "" FORCE)

# VOIP mode for voice; enables SILK, CELT, and the hybrid mode
set(OPUS_APPLICATION_VOIP ON CACHE BOOL "" FORCE)

add_subdirectory(${OPUS_SOURCE_DIR} opus_build)

# --- opus_jni (PocketMic JNI wrapper) ---

add_library(pocketmic_jni SHARED opus_jni.c)

target_include_directories(pocketmic_jni PRIVATE
    ${OPUS_SOURCE_DIR}/include
)

target_link_libraries(pocketmic_jni PRIVATE opus)

# Link Android log library for __android_log_print
find_library(log-lib log)
target_link_libraries(pocketmic_jni PRIVATE ${log-lib})
