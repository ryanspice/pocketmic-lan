package com.canopydigital.pocketmic

/** Called under ControlChannel's monitor so ownership and socket operations are atomic. */
internal class ControlOwnership {
    private val visible = mutableSetOf<Any>()
    private val streams = mutableSetOf<Any>()
    private var runOwner: Any? = null
    private var runToken: Any? = null

    val idleAllowed: Boolean get() = visible.isNotEmpty() && streams.isEmpty()
    val unused: Boolean get() = visible.isEmpty() && streams.isEmpty()
    fun acquireUi(owner: Any) { visible.add(owner) }
    fun releaseUi(owner: Any) { visible.remove(owner) }
    fun acquireStream(owner: Any) { streams.add(owner) }
    fun releaseStream(owner: Any) {
        streams.remove(owner)
        if (runOwner === owner) { runOwner = null; runToken = null }
    }
    fun ownsUi(owner: Any): Boolean = owner in visible
    fun ownsStream(owner: Any): Boolean = owner in streams
    fun beginRun(owner: Any, token: Any) {
        if (ownsStream(owner)) { runOwner = owner; runToken = token }
    }
    fun ownsRun(owner: Any, token: Any): Boolean = ownsStream(owner) && runOwner === owner && runToken === token
}
