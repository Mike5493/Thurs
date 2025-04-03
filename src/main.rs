mod engine;
mod map;
mod player;
mod raycaster;

use raylib::prelude::*;
use engine::run_game;

fn main() {
    let (mut rl, thread) = raylib::init()
        .size(800, 600)
        .title("THURS")
        .build();
    rl.set_target_fps(60);
    run_game(&mut rl, &thread);
}
