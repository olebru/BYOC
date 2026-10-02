# Changelog

## [1.6.0](https://github.com/olebru/exuarch/compare/v1.5.0...v1.6.0) (2026-10-02)


### Features

* a loading splash with a 16 bit register counting in binary ([4fc1875](https://github.com/olebru/exuarch/commit/4fc18754216c927337a6b47b4e55e80822d5a1b6))
* **dsp:** close in on the top of the Mandelbrot set, in 4.12 fixed point ([4fc1875](https://github.com/olebru/exuarch/commit/4fc18754216c927337a6b47b4e55e80822d5a1b6))

## [1.5.0](https://github.com/olebru/exuarch/compare/v1.4.0...v1.5.0) (2026-10-02)


### Features

* add DSP-16, RISC-16 with a multiplier, drawing the Mandelbrot set ([#35](https://github.com/olebru/exuarch/issues/35)) ([a5722ef](https://github.com/olebru/exuarch/commit/a5722efe6f30950d1d4a745dc9d7d4ffcd647ee6))


### Bug fixes

* list WORM-16 as a ludicrous machine ([#33](https://github.com/olebru/exuarch/issues/33)) ([c4bf272](https://github.com/olebru/exuarch/commit/c4bf272e6e3004508727a1ec28152400712b86b3))

## [1.4.0](https://github.com/olebru/exuarch/compare/v1.3.0...v1.4.0) (2026-10-02)


### Features

* add WORM-16, a machine with no jumps whose program crawls round a ring ([55f99ff](https://github.com/olebru/exuarch/commit/55f99ff80ffd426c5ce7eb7783627adede525dc1))
* **cisc:** add a program that rewrites its own instructions ([#31](https://github.com/olebru/exuarch/issues/31)) ([754c511](https://github.com/olebru/exuarch/commit/754c5114ada0d60086c842786a449147c445a981))


### Bug fixes

* **run:** show the instruction memory holds now in Now executing, not only what was assembled ([55f99ff](https://github.com/olebru/exuarch/commit/55f99ff80ffd426c5ce7eb7783627adede525dc1))


### Documentation

* describe how Umami tells visits apart ([#29](https://github.com/olebru/exuarch/issues/29)) ([a63d0d2](https://github.com/olebru/exuarch/commit/a63d0d2f924511c35adba6935d228d45e30ac4b5))

## [1.3.0](https://github.com/olebru/exuarch/compare/v1.2.0...v1.3.0) (2026-10-02)


### Features

* introduce ExµArch to phone visitors and point them to a computer ([#27](https://github.com/olebru/exuarch/issues/27)) ([68b17d8](https://github.com/olebru/exuarch/commit/68b17d8399beb1120d3bfd38ed5521993a2d8292))


### Documentation

* explain how ExµArch relates to real hardware ([#26](https://github.com/olebru/exuarch/issues/26)) ([d659aaf](https://github.com/olebru/exuarch/commit/d659aaf247820ab149cc3dc31fa27f119ac45d6d))

## [1.2.0](https://github.com/olebru/exuarch/compare/v1.1.0...v1.2.0) (2026-10-02)


### Features

* **run:** put the Run view's panels in two columns that resize, fold away and take dragged panels ([d346e42](https://github.com/olebru/exuarch/commit/d346e420e94dff367bc5fe56cb06f545f2fdf7f8))
* **run:** split the panels below into two groups of tabs side by side ([d346e42](https://github.com/olebru/exuarch/commit/d346e420e94dff367bc5fe56cb06f545f2fdf7f8))


### Bug fixes

* moving parts in the hardware design no longer marks a machine as edited ([d346e42](https://github.com/olebru/exuarch/commit/d346e420e94dff367bc5fe56cb06f545f2fdf7f8))
* **run:** fit the LCD to its panel instead of scrolling it ([d346e42](https://github.com/olebru/exuarch/commit/d346e420e94dff367bc5fe56cb06f545f2fdf7f8))
* **run:** show the drawing at 100% and only zoom out to fit ([d346e42](https://github.com/olebru/exuarch/commit/d346e420e94dff367bc5fe56cb06f545f2fdf7f8))

## [1.1.0](https://github.com/olebru/exuarch/compare/v1.0.0...v1.1.0) (2026-10-02)


### Features

* tag releases with release-please and deploy production only from them ([#22](https://github.com/olebru/exuarch/issues/22)) ([5bd20e5](https://github.com/olebru/exuarch/commit/5bd20e5e126ef3a020c72e1f14890a0126f4ae3c))
