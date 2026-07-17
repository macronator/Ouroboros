# Ouroboros

An open-source proxy and automation toolkit for Dark Ages (7.41).

## Open source & attribution

Ouroboros is an **open-source project**.

If you've spent any time around Dark Ages tooling, you'll notice features and
concepts here that resemble other third-party projects — including Accolade,
Ascend, Deity, Ditto, Dojo, ETDA, Porygon, ProxyBase, SleepHunter, Zeus, and
some of my own earlier work — as well as the many open-source Dark Ages private
servers and independent re-writes of the game's client/engine. That overlap is
expected: these tools all target the same game and the same network protocol,
so they naturally end up solving the same problems in similar ways.

To be clear about how that works in this project:

> **No code written by anyone else is copied into Ouroboros.** Where an idea or
> approach is shared with another tool, it is always re-implemented from scratch
> in Ouroboros's own style. Same or similar *concepts* — our own *code*.

## References

Ouroboros frequently references open-source work from the Dark Ages community,
alongside our own reverse-engineering of the live 7.41 client and protocol:

- **Chaos** (ChaosLib) — the networking / packet / cryptography stack Ouroboros builds on.
- **DALib** — Dark Ages client data-file and asset decoding.
- **SiLo's Arbiter** and its accompanying **reverse-engineering repo** — protocol and
  packet reference for live Dark Ages.
- Our own reverse-engineering of the live Dark Ages client.

These are used as references for how things work — never as a source of copied code.

## Credits

To name a few — thanks to **wren, SiLo, Sichii, Bivins, Acht, Jeremy**, and everyone else
whose open-source work and research helped bring this together.

## License

See [LICENSE](LICENSE).
